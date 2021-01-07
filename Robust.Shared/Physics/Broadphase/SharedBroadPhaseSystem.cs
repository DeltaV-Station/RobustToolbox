﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.GameObjects.Components.Map;
using Robust.Shared.GameObjects.Components.Transform;
using Robust.Shared.GameObjects.EntitySystemMessages;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Robust.Shared.Physics.Broadphase
{
    public interface IBroadPhaseManager
    {
        // More or less a clone of IBroadPhase but this one doesn't really care about what map it's on.

        void AddBody(PhysicsComponent component);

        void RemoveBody(PhysicsComponent component);

        void SynchronizeFixtures(PhysicsComponent component, PhysicsTransform xf1, PhysicsTransform xf2);

        void DestroyProxies(Fixture fixture);

        void TouchProxy(FixtureProxy proxy);

        void UpdatePairs(MapId mapId, BroadphaseDelegate callback);

        bool TestOverlap(FixtureProxy proxyA, FixtureProxy proxyB);

        /// <summary>
        ///     Call when a fixture is added directly to a body that's already in broadphase.
        /// </summary>
        /// <param name="fixture"></param>
        void CreateProxies(Fixture fixture);

        IEnumerable<RayCastResults> IntersectRayWithPredicate(MapId mapId, CollisionRay ray, Func<IEntity, bool>? predicate = null, bool returnOnFirstHit = true);

        IEnumerable<RayCastResults> IntersectRay(MapId mapId, CollisionRay ray, IEntity? ignoredEnt = null, bool returnOnFirstHit = true);

        float IntersectRayPenetration(MapId mapId, CollisionRay ray, IEntity? ignoredEnt = null);
    }

    public sealed class SharedBroadPhaseSystem : EntitySystem, IBroadPhaseManager
    {
        /*
         * That's right both the system implements IBroadPhase and also each grid has its own as well.
         * The reason for this is other stuff should just be able to check for broadphase with no regard
         * for the concept of grids, whereas internally this needs to worry about it.
         */

        // TODO: Have message for stuff inserted into containers
        // Anything in a container is removed from the graph and anything removed from a container is added to the graph.

        // TODO: This thing is going to memory leak like a motherfucker for space so need to handle that.
        // Ideally you'd pool space chunks.

        [Dependency] private readonly IMapManager _mapManager = default!;

        private readonly Dictionary<MapId, Dictionary<GridId, IBroadPhase>> _graph = new();

        private Dictionary<PhysicsComponent, List<IBroadPhase>> _lastBroadPhases = new(1);

        // Raycasts
        private RayCastReportFixtureDelegate? _rayCastDelegateTmp;

        private Queue<EntMapIdChangedMessage> _queuedMapChanges = new Queue<EntMapIdChangedMessage>();

        private IEnumerable<IBroadPhase> BroadPhases()
        {
            foreach (var (_, grids) in _graph)
            {
                foreach (var (_, broad) in grids)
                {
                    yield return broad;
                }
            }
        }

        public IBroadPhase? GetBroadPhase(MapId mapId, GridId gridId)
        {
            if (mapId == MapId.Nullspace)
                return null;

            return _graph[mapId][gridId];
        }

        // Look for now I've hardcoded grids
        public IEnumerable<(IBroadPhase Broadphase, GridId GridId)> GetBroadphases(PhysicsComponent body)
        {
            // TODO: Snowflake grids here
            var grids = _graph[body.Owner.Transform.MapID];

            foreach (var gridId in _mapManager.FindGridIdsIntersecting(body.Owner.Transform.MapID, body.WorldAABB, true))
            {
                yield return (grids[gridId], gridId);
            }
        }

        public bool TryCollideRect(Box2 collider, MapId mapId, bool approximate = true)
        {
            var state = (collider, mapId, found: false);

            foreach (var gridId in _mapManager.FindGridIdsIntersecting(mapId, collider, true))
            {
                var gridCollider = _mapManager.GetGrid(gridId).WorldToLocal(collider.Center);
                var gridBox = collider.Translated(gridCollider);

                _graph[mapId][gridId].QueryAabb(ref state, (ref (Box2 collider, MapId map, bool found) state, in FixtureProxy proxy) =>
                {
                    if (proxy.Fixture.CollisionLayer == 0x0)
                        return true;

                    if (proxy.AABB.Intersects(gridBox))
                    {
                        state.found = true;
                        return false;
                    }
                    return true;
                }, gridBox, approximate);
            }

            return state.found;
        }

        public bool TestOverlap(FixtureProxy proxyA, FixtureProxy proxyB)
        {
            // TODO: This should only ever be called on the same grid I think so maybe just assert
            var mapA = proxyA.Fixture.Body.Owner.Transform.MapID;
            var mapB = proxyB.Fixture.Body.Owner.Transform.MapID;

            if (mapA != mapB)
                return false;

            return proxyA.AABB.Intersects(proxyB.AABB);
        }

        public void UpdatePairs(MapId mapId, BroadphaseDelegate callback)
        {
            foreach (var (_, broadPhase) in _graph[mapId])
            {
                broadPhase.UpdatePairs(callback);
            }
        }

        // TODO: Probably just snowflake grids.

        // TODO: For now I'm just using DynamicTree

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MoveEvent>(HandlePhysicsMove);
            SubscribeLocalEvent<EntMapIdChangedMessage>(QueueMapChange);
            _mapManager.OnGridCreated += HandleGridCreated;
            _mapManager.OnGridRemoved += HandleGridRemoval;
            _mapManager.MapCreated += HandleMapCreated;
            UpdatesBefore.Add(typeof(SharedPhysicsSystem));
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var handling = false;

            if (_queuedMapChanges.Count > 0)
            {
                Logger.Debug($"Handling MapId changes for {_queuedMapChanges.Count} entities");
                handling = true;
            }

            while (_queuedMapChanges.Count > 0)
            {
                HandleMapChange(_queuedMapChanges.Dequeue());
                Logger.Debug($"{_queuedMapChanges.Count} remaining");
            }

            if (handling)
            {
                Logger.Debug("Handled MapId changes");
            }
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _mapManager.OnGridCreated -= HandleGridCreated;
            _mapManager.OnGridRemoved -= HandleGridRemoval;
            _mapManager.MapCreated -= HandleMapCreated;
        }

        private void HandlePhysicsMove(MoveEvent moveEvent)
        {
            if (!moveEvent.Sender.TryGetComponent(out PhysicsComponent? physicsComponent))
                return;

            physicsComponent.SynchronizeFixtures();
        }

        private void QueueMapChange(EntMapIdChangedMessage message)
        {
            _queuedMapChanges.Enqueue(message);
        }

        /// <summary>
        ///     Handles map changes for bodies completely
        /// </summary>
        /// <param name="message"></param>
        private void HandleMapChange(EntMapIdChangedMessage message)
        {
            if (message.Entity.Deleted ||
                !message.Entity.TryGetComponent(out PhysicsComponent? physicsComponent) ||
                !_lastBroadPhases.TryGetValue(physicsComponent, out var broadPhases))
            {
                return;
            }

            if (Get<SharedPhysicsSystem>().Maps.TryGetValue(message.OldMapId, out var oldMap))
            {
                oldMap.Remove(physicsComponent);
            }

            if (Get<SharedPhysicsSystem>().Maps.TryGetValue(message.Entity.Transform.MapID, out var newMap))
            {
                newMap.Add(physicsComponent);
            }

            var newBroadPhases = GetBroadphases(physicsComponent).ToList();
            var oldBroadPhases = broadPhases.Where(o => !newBroadPhases.Select(b => b.Broadphase).Contains(o));

            /* TODO: Seems to dupe DestroyProxies ya dumbass
            if (message.OldMapId != MapId.Nullspace)
            {
                foreach (var broadPhase in oldBroadPhases)
                {
                    var proxies = physicsComponent.GetProxies(GetGridId(broadPhase));

                    foreach (var proxy in proxies)
                    {
                        broadPhase.RemoveProxy(proxy.ProxyId);
                    }
                }
            }
            */
            foreach (var fixture in physicsComponent.FixtureList)
            {
                fixture.DestroyProxies();
            }

            _lastBroadPhases[physicsComponent].Clear();
            _lastBroadPhases[physicsComponent] = new List<IBroadPhase>();

            if (physicsComponent.Owner.Transform.MapID != MapId.Nullspace)
            {
                foreach (var (broadPhase, gridId) in newBroadPhases)
                {
                    var transform = physicsComponent.GetTransform();

                    _lastBroadPhases[physicsComponent].Add(broadPhase);
                    foreach (var fixture in physicsComponent.FixtureList)
                    {
                        fixture.CreateProxies(gridId, transform);
                    }

                    var proxies = physicsComponent.GetProxies(gridId);

                    for (var i = 0; i < proxies.Length; i++)
                    {
                        ref var proxy = ref proxies[i];
                        proxy.ProxyId = broadPhase.AddProxy(proxy);
                        DebugTools.Assert(proxy.ProxyId != DynamicTree.Proxy.Free);
                    }
                }
            }
        }

        private void HandleGridCreated(GridId gridId)
        {
            var mapId = _mapManager.GetGrid(gridId).ParentMapId;

            if (!_graph.TryGetValue(mapId, out var grids))
            {
                grids = new Dictionary<GridId, IBroadPhase>();
                _graph[mapId] = grids;
            }

            grids[gridId] = new DynamicTreeBroadPhase(mapId, gridId);
        }

        private void HandleMapCreated(object? sender, MapEventArgs eventArgs)
        {
            _graph[eventArgs.Map] = new Dictionary<GridId, IBroadPhase>()
            {
                {
                    GridId.Invalid,
                    new DynamicTreeBroadPhase(eventArgs.Map, GridId.Invalid)
                }
            };

        }

        private void HandleGridRemoval(GridId gridId)
        {
            var mapId = _mapManager.GetGrid(gridId).ParentMapId;
            _graph[mapId].Remove(gridId);
        }

        public void AddBody(PhysicsComponent component)
        {
            var mapId = component.Owner.Transform.MapID;

            if (mapId == MapId.Nullspace)
            {
                _lastBroadPhases[component] = new List<IBroadPhase>();
                return;
            }

            var grids = _graph[mapId];
            var fixtures = component.FixtureList;
            var transform = component.GetTransform();
            _lastBroadPhases[component] = new List<IBroadPhase>();

            foreach (var gridId in _mapManager.FindGridIdsIntersecting(mapId,
                component.WorldAABB, true))
            {
                var broadPhase = grids[gridId];

                foreach (var fixture in fixtures)
                {
                    var proxies = fixture.CreateProxies(gridId, transform);
                    for (var i = 0; i < fixture.ProxyCount; i++)
                    {
                        ref var proxy = ref proxies[i];
                        proxy.ProxyId = broadPhase.AddProxy(proxy);
                        DebugTools.Assert(proxy.ProxyId != DynamicTree.Proxy.Free);
                    }
                }

                _lastBroadPhases[component].Add(broadPhase);
            }
        }

        public void RemoveBody(PhysicsComponent component)
        {
            if (!_lastBroadPhases.TryGetValue(component, out var broadPhases))
            {
                // TODO: Log warning
                return;
            }

            foreach (var broadPhase in broadPhases)
            {
                foreach (var fixture in component.FixtureList)
                {
                    foreach (var proxy in fixture.Proxies[GetGridId(broadPhase)])
                    {
                        broadPhase.RemoveProxy(proxy.ProxyId);
                    }
                }
            }

            _lastBroadPhases.Remove(component);
        }

        public void SynchronizeFixtures(PhysicsComponent component, PhysicsTransform xf1, PhysicsTransform xf2)
        {
            var mapId = component.Owner.Transform.MapID;

            if (mapId == MapId.Nullspace)
                return;

            if (!_lastBroadPhases.TryGetValue(component, out var broadPhases))
            {
                broadPhases = new List<IBroadPhase>();
                _lastBroadPhases[component] = broadPhases;
            }

            var oldGridIds = broadPhases.Select(o => GetGridId(o)).ToList();
            var newGridIds = new List<GridId>();
            foreach (var gridId in _mapManager.FindGridIdsIntersecting(mapId,
                component.WorldAABB, true))
            {
                newGridIds.Add(gridId);
            }

            // Remove old broadPhases
            foreach (var gridId in oldGridIds.Where(o => !newGridIds.Contains(o)))
            {
                var broadPhase = GetBroadPhase(mapId, gridId);
                Debug.Assert(broadPhase != null);

                foreach (var fixture in component.FixtureList)
                {
                    foreach (var proxy in fixture.Proxies[gridId])
                    {
                        broadPhase.RemoveProxy(proxy.ProxyId);
                    }
                }

                _lastBroadPhases[component].Remove(broadPhase);
            }

            // Move on existing ones
            foreach (var gridId in newGridIds.Where(o => oldGridIds.Contains(o)))
            {
                var broadPhase = GetBroadPhase(mapId, gridId);
                Debug.Assert(broadPhase != null);

                foreach (var fixture in component.FixtureList)
                {
                    for (var i = 0; i < fixture.ProxyCount; i++)
                    {
                        ref var proxy = ref fixture.Proxies[gridId][i];
                        proxy.AABB = SynchronizeAABB(proxy, xf1, xf2);
                        var displacement = xf2.Position - xf1.Position;
                        broadPhase.MoveProxy(proxy.ProxyId, ref proxy.AABB, displacement);
                    }
                }
            }

            // Add to new ones
            foreach (var gridId in newGridIds.Where(o => !oldGridIds.Contains(o)))
            {
                var broadPhase = GetBroadPhase(mapId, gridId);
                Debug.Assert(broadPhase != null);
                var transform = component.GetTransform();

                if (gridId != GridId.Invalid)
                {
                    transform.Position -= _mapManager.GetGrid(gridId).WorldPosition;
                }

                foreach (var fixture in component.FixtureList)
                {
                    var proxies = fixture.CreateProxies(gridId, transform);

                    for (var i = 0; i < fixture.ProxyCount; i++)
                    {
                        ref var proxy = ref proxies[i];
                        proxy.ProxyId = broadPhase.AddProxy(proxy);
                        DebugTools.Assert(proxy.ProxyId != DynamicTree.Proxy.Free);
                    }
                }

                _lastBroadPhases[component].Add(broadPhase);
            }
        }

        private Box2 SynchronizeAABB(FixtureProxy proxy, PhysicsTransform xf1, PhysicsTransform xf2)
        {
            var aabb = proxy.Fixture.Shape.ComputeAABB(xf1, proxy.ChildIndex);
            return aabb.Combine(proxy.Fixture.Shape.ComputeAABB(xf2, proxy.ChildIndex));
        }

        public void DestroyProxies(Fixture fixture)
        {
            var mapid = fixture.Body.Owner.Transform.MapID;

            foreach (var (gridId, proxies) in fixture.Proxies)
            {
                var broadPhase = GetBroadPhase(mapid, gridId);
                Debug.Assert(broadPhase != null);

                foreach (var proxy in proxies)
                {
                    broadPhase.RemoveProxy(proxy.ProxyId);
                }
            }
        }

        public void TouchProxy(FixtureProxy proxy)
        {
            throw new NotImplementedException();

            foreach (var broadPhase in _lastBroadPhases[proxy.Fixture.Body])
            {
                //broadPhase.TouchProxy(proxy);
            }
        }

        private GridId GetGridId(IBroadPhase broadPhase)
        {
            foreach (var (_, grids) in _graph)
            {
                foreach (var (gridId, broad) in grids)
                {
                    if (broadPhase == broad)
                    {
                        return gridId;
                    }
                }
            }

            throw new InvalidOperationException("Unable to find GridId for broadPhase");
        }

        public void CreateProxies(Fixture fixture)
        {
            foreach (var broadPhase in _lastBroadPhases[fixture.Body])
            {
                var gridId = GetGridId(broadPhase);

                var proxies = fixture.CreateProxies(gridId, fixture.Body.GetTransform(), _mapManager);

                for (var i = 0; i < fixture.ProxyCount; i++)
                {
                    ref var proxy = ref proxies[i];
                    proxy.ProxyId = broadPhase.AddProxy(proxy);
                }
            }
        }

        public IEnumerable<IEntity> GetCollidingEntities(IPhysBody body, Vector2 offset, bool approximate = true)
        {
            var modifiers = body.Owner.GetAllComponents<ICollideSpecial>();
            var entities = new List<IEntity>();

            var state = (body, modifiers, entities);

            foreach (var broadPhase in _lastBroadPhases[(PhysicsComponent) body])
            {
                foreach (var fixture in body.FixtureList)
                {
                    foreach (var proxy in fixture.Proxies[GetGridId(broadPhase)])
                    {
                        broadPhase.QueryAabb(ref state,
                            (ref (IPhysBody body, IEnumerable<ICollideSpecial> modifiers, List<IEntity> entities) state,
                                in FixtureProxy other) =>
                            {
                                if (body.Owner.Deleted)
                                    return true;

                                if ((proxy.Fixture.CollisionMask & other.Fixture.CollisionLayer) != 0)
                                {
                                    var preventCollision = false;
                                    var otherModifiers = other.Fixture.Body.Owner.GetAllComponents<ICollideSpecial>();
                                    foreach (var modifier in state.modifiers)
                                    {
                                        preventCollision |= modifier.PreventCollide(other.Fixture.Body);
                                    }
                                    foreach (var modifier in otherModifiers)
                                    {
                                        preventCollision |= modifier.PreventCollide(body);
                                    }

                                    if (preventCollision)
                                        return true;

                                    state.entities.Add(body.Owner);
                                }
                                return true;
                            }, proxy.AABB, approximate);
                    }
                }
            }

            return entities;
        }

        public IEnumerable<PhysicsComponent> GetCollidingEntities(MapId mapId, in Box2 worldAABB)
        {
            var bodies = new HashSet<PhysicsComponent>();

            foreach (var gridId in _mapManager.FindGridIdsIntersecting(mapId, worldAABB, true))
            {
                foreach (var proxy in _graph[mapId][gridId].QueryAabb(worldAABB, false))
                {
                    if (bodies.Contains(proxy.Fixture.Body)) continue;
                    bodies.Add(proxy.Fixture.Body);
                }
            }

            return bodies;
        }

        // This is dirty but so is a lot of other shit so it'll get refactored at some stage tm
        public IEnumerable<PhysicsComponent> GetAwakeBodies(MapId mapId, GridId gridId)
        {
            var map = Get<SharedPhysicsSystem>().Maps[mapId];

            foreach (var body in map.AwakeBodySet)
            {
                if (body.Owner.Transform.GridID == gridId)
                    yield return body;
            }
        }

        public IEnumerable<RayCastResults> IntersectRayWithPredicate(MapId mapId, CollisionRay ray,
            Func<IEntity, bool>? predicate = null, bool returnOnFirstHit = true)
        {
            List<RayCastResults> results = new();

            var rayBox = new Box2();

            foreach (var gridId in _mapManager.FindGridIdsIntersecting(mapId, rayBox, true))
            {
                var gridOrigin = _mapManager.GetGrid(gridId).WorldPosition;
                var translatedRay = new CollisionRay(ray.Start + gridOrigin, ray.End + gridOrigin, ray.CollisionMask);

                _graph[mapId][gridId].QueryRay((in FixtureProxy proxy, in Vector2 point, float distFromOrigin) =>
                {
                    if (returnOnFirstHit && results.Count > 0)
                        return true;

                    if (distFromOrigin > ray.Distance)
                        return true;

                    if ((proxy.Fixture.CollisionLayer & ray.CollisionMask) == 0x0)
                        return true;

                    if (!proxy.Fixture.RayCast(out _, ref ray, proxy.ChildIndex))
                        return true;

                    var entity = proxy.Fixture.Body.Owner;

                    if (predicate != null && predicate.Invoke(entity))
                        return true;

                    var result = new RayCastResults(distFromOrigin, point, entity);
                    results.Add(result);
                    //DebugDrawRay?.Invoke(new DebugRayData(ray, maxLength, result));
                    return true;
                }, translatedRay);
            }

            results.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return results;
        }

        /// <inheritdoc />
        public IEnumerable<RayCastResults> IntersectRay(MapId mapId, CollisionRay ray, IEntity? ignoredEnt = null, bool returnOnFirstHit = true)
            => IntersectRayWithPredicate(mapId, ray, entity => entity == ignoredEnt, returnOnFirstHit);

        /// <inheritdoc />
        public float IntersectRayPenetration(MapId mapId, CollisionRay ray, IEntity? ignoredEnt = null)
        {
            var penetration = 0f;
            var bottomLeft = new Vector2(Math.Min(ray.Start.X, ray.End.X), Math.Min(ray.Start.Y, ray.End.Y));
            var topRight = new Vector2(Math.Max(ray.Start.X, ray.End.X), Math.Max(ray.Start.Y, ray.End.Y));
            var rayBox = new Box2(bottomLeft, topRight);

            foreach (var gridId in _mapManager.FindGridIdsIntersecting(mapId, rayBox, true))
            {
                var gridOrigin = _mapManager.GetGrid(gridId).WorldPosition;
                var translatedRay = new Ray(ray.Start + gridOrigin, ray.End + gridOrigin);

                _graph[mapId][gridId].QueryRay((in FixtureProxy proxy, in Vector2 point, float distFromOrigin) =>
                {
                    if (distFromOrigin > ray.Distance)
                        return true;

                    if ((proxy.Fixture.CollisionLayer & ray.CollisionMask) == 0x0)
                        return true;

                    var newRay = new CollisionRay(point + translatedRay.Direction * proxy.AABB.Size.Length * 2,
                        -translatedRay.Direction, 0x0);

                    // TODO: Somewhat sketch of this
                    if (proxy.Fixture.RayCast(out var hit, ref newRay, proxy.ChildIndex))
                    {
                        var hitPos = (newRay.End - newRay.Start) * hit.Fraction;
                        penetration += (point - hitPos).Length;
                    }

                    return true;
                }, translatedRay);

            }
            return penetration;
        }
    }
}

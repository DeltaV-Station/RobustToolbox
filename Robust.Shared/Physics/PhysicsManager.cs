﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.Interfaces.Physics;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Robust.Shared.Physics
{
    /// <inheritdoc />
    public class PhysicsManager : IPhysicsManager
    {
        private readonly ConcurrentDictionary<MapId,BroadPhase> _treesPerMap =
            new ConcurrentDictionary<MapId, BroadPhase>();

        private BroadPhase this[MapId mapId] => _treesPerMap.GetOrAdd(mapId, _ => new BroadPhase());

        /// <summary>
        ///     returns true if collider intersects a physBody under management. Does not trigger Bump.
        /// </summary>
        /// <param name="collider">Rectangle to check for collision</param>
        /// <param name="map">Map ID to filter</param>
        /// <returns></returns>
        public bool TryCollideRect(Box2 collider, MapId map)
        {
            foreach (var body in this[map].Query(collider))
            {
                if (!body.CanCollide || body.CollisionLayer == 0x0)
                    continue;

                if (body.MapID == map &&
                    body.WorldAABB.Intersects(collider))
                    return true;
            }

            return false;
        }

        public Vector2 CalculateNormal(ICollidableComponent target, ICollidableComponent source)
        {
            var manifold = target.WorldAABB.Intersect(source.WorldAABB);
            if (manifold.IsEmpty()) return Vector2.Zero;
            if (manifold.Height > manifold.Width)
            {
                // X is the axis of seperation
                var leftDist = source.WorldAABB.Right - target.WorldAABB.Left;
                var rightDist = target.WorldAABB.Right - source.WorldAABB.Left;
                return new Vector2(manifold.Width * leftDist > rightDist ? 1 : -1, 0);
            }
            else
            {
                // Y is the axis of seperation
                var bottomDist = source.WorldAABB.Top - target.WorldAABB.Bottom;
                var topDist = target.WorldAABB.Top - source.WorldAABB.Bottom;
                return new Vector2(0, manifold.Height * bottomDist > topDist ? 1 : -1);
            }
        }


        public Vector2 CalculateCollisionImpulse(ICollidableComponent target, ICollidableComponent source, Vector2 targetVel, Vector2 sourceVel, float targetMass, float sourceMass)
        {
            var relativeVelocity = sourceVel - targetVel;
            var normal = CalculateNormal(target, source);
            var contactVel = Vector2.Dot(relativeVelocity, normal);
            if (contactVel == 0)
            {
                return normal * 10.0f;
            }
            if (contactVel > 0)
            {
                return Vector2.Zero;
            }
            const float restitution = 0.0f;
            var targetImpulse = normal * (1 + restitution) * contactVel;
            if (sourceMass == 0.0f)
            {
                targetImpulse /= 1 / targetMass;
            }
            else
            {
                targetImpulse /= 1 / targetMass + 1 / sourceMass;
            }
            return targetImpulse;
        }

        public IEnumerable<IEntity> GetCollidingEntities(IPhysBody physBody, Vector2 offset, bool approximate = true)
        {
            var modifiers = physBody.Owner.GetAllComponents<ICollideSpecial>();
            foreach ( var body in this[physBody.MapID].Query(physBody.WorldAABB, approximate))
            {
                if (body.Owner.Deleted) {
                    continue;
                }

                if (CollidesOnMask(physBody, body))
                {
                    var preventCollision = false;
                    var otherModifiers = body.Owner.GetAllComponents<ICollideSpecial>();
                    foreach (var modifier in modifiers)
                    {
                        preventCollision |= modifier.PreventCollide(body);
                    }
                    foreach (var modifier in otherModifiers)
                    {
                        preventCollision |= modifier.PreventCollide(physBody);
                    }

                    if (preventCollision) continue;
                    yield return body.Owner;
                }
            }
        }

        public bool IsColliding(IPhysBody body, Vector2 offset)
        {
            return GetCollidingEntities(body, offset).Any();
        }

        public static bool CollidesOnMask(IPhysBody a, IPhysBody b)
        {
            if (a == b)
                return false;

            if (!a.CanCollide || !b.CanCollide)
                return false;

            if ((a.CollisionMask & b.CollisionLayer) == 0x0 &&
                (b.CollisionMask & a.CollisionLayer) == 0x0)
                return false;

            return true;
        }

        /// <summary>
        ///     Adds a physBody to the manager.
        /// </summary>
        /// <param name="physBody"></param>
        public void AddBody(IPhysBody physBody)
        {
            if (!this[physBody.MapID].Add(physBody))
            {
                Logger.WarningS("phys", $"PhysicsBody already registered! {physBody.Owner}");
            }
        }

#pragma warning disable 649
        [Dependency] private readonly IMapManager _mapManager;
#pragma warning restore 649

        /// <summary>
        ///     Removes a physBody from the manager
        /// </summary>
        /// <param name="physBody"></param>
        public void RemoveBody(IPhysBody physBody)
        {
            var removed = false;

            if (physBody.Owner.Deleted || physBody.Owner.Transform.Deleted)
            {
                foreach (var mapId in _mapManager.GetAllMapIds())
                {
                    removed = this[mapId].Remove(physBody);

                    if (removed)
                    {
                        break;
                    }
                }
            }

            if (!removed)
            {
                try
                {
                    removed = this[physBody.MapID].Remove(physBody);
                }
                catch (InvalidOperationException)
                {
                    // TODO: TryGetMapId or something
                    foreach (var mapId in _mapManager.GetAllMapIds())
                    {
                        removed = this[mapId].Remove(physBody);

                        if (removed)
                        {
                            break;
                        }
                    }
                }
            }

            if (!removed)
            {
                foreach (var mapId in _mapManager.GetAllMapIds())
                {
                    removed = this[mapId].Remove(physBody);

                    if (removed)
                    {
                        break;
                    }
                }
            }

            if (!removed)
                Logger.WarningS("phys", $"Trying to remove unregistered PhysicsBody! {physBody.Owner}");
        }

        /// <inheritdoc />
        public RayCastResults IntersectRayWithPredicate(MapId mapId, CollisionRay ray, float maxLength = 50, Func<IEntity, bool> predicate = null)
        {
            RayCastResults result = default;

            this[mapId].Query((ref IPhysBody body, in Vector2 point, float distFromOrigin) => {

                if (distFromOrigin > maxLength)
                {
                    return true;
                }

                if (predicate.Invoke(body.Owner))
                {
                    return true;
                }

                if (!body.CanCollide)
                {
                    return true;
                }

                if ((body.CollisionLayer & ray.CollisionMask) == 0x0)
                {
                    return true;
                }

                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (result.Distance != 0f && distFromOrigin > result.Distance)
                {
                    return true;
                }

                result = new RayCastResults(distFromOrigin, point, body.Owner);

                return true;
            }, ray.Position, ray.Direction);

            DebugDrawRay?.Invoke(new DebugRayData(ray, maxLength, result));

            return result;
        }

        /// <inheritdoc />
        public RayCastResults IntersectRay(MapId mapId, CollisionRay ray, float maxLength = 50, IEntity ignoredEnt = null)
            => IntersectRayWithPredicate(mapId, ray, maxLength, entity => entity == ignoredEnt);

        public event Action<DebugRayData> DebugDrawRay;

        public IEnumerable<(IPhysBody, IPhysBody)> GetCollisions()
        {
            foreach (var mapId in _mapManager.GetAllMapIds())
            {
                foreach (var collision in this[mapId].GetCollisions(true))
                {
                    var (a, b) = collision;

                    if (!a.CanCollide || !b.CanCollide)
                    {
                        continue;
                    }

                    if (((a.CollisionLayer & b.CollisionMask) == 0x0)
                        ||(b.CollisionLayer & a.CollisionMask) == 0x0)
                    {
                        continue;
                    }

                    if (!a.WorldAABB.Intersects(b.WorldAABB))
                    {
                        continue;
                    }

                    yield return collision;
                }
            }
        }

        public bool Update(IPhysBody collider)
            => this[collider.MapID].Update(collider);

        public void RemovedFromMap(IPhysBody body, MapId mapId)
        {
            this[mapId].Remove(body);
        }

        public void AddedToMap(IPhysBody body, MapId mapId)
        {
            this[mapId].Add(body);
        }
    }
}

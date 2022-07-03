using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Broadphase;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Dynamics.Contacts;
using Robust.Shared.Utility;

namespace Robust.Shared.Physics
{
    public abstract class SharedBroadphaseSystem : EntitySystem
    {
        [Dependency] private readonly IMapManagerInternal _mapManager = default!;
        [Dependency] private readonly IPhysicsManager _physicsManager = default!;
        [Dependency] private readonly SharedTransformSystem _xformSys = default!;
        [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;

        private ISawmill _logger = default!;

        private const int MinimumBroadphaseCapacity = 256;

        // We queue updates rather than handle them immediately for multiple reasons
        // A) Entity initializing may call several events which only need handling once so we'd need to add a bunch of code to account for what stage of initializing they're at
        // B) It's faster for instances like MoveEvent and RotateEvent both being issued

        /*
         * Okay so Box2D has its own "MoveProxy" stuff so you can easily find new contacts when required.
         * Our problem is that we have nested broadphases (rather than being on separate maps) which makes this
         * not feasible because a body could be intersecting 2 broadphases.
         * Hence we need to check which broadphases it does intersect and checkar for colliding bodies.
         */

        // We keep 2 move buffers as we need to handle the broadphase moving behavior first.
        // This is because we'll chuck anything the broadphase moves over onto the movebuffer so contacts can be generated.
        private Dictionary<MapId, Dictionary<FixtureProxy, Box2>> _moveBuffer = new();

        // Caching for FindNewContacts
        private Dictionary<FixtureProxy, HashSet<FixtureProxy>> _pairBuffer = new(64);
        private Dictionary<FixtureProxy, Box2> _gridMoveBuffer = new(64);
        private List<FixtureProxy> _queryBuffer = new(32);

        private List<MapGrid> _gridsPool = new(8);

        /// <summary>
        /// How much to expand bounds by to check cross-broadphase collisions.
        /// Ideally you want to set this to your largest body size.
        /// This only has a noticeable performance impact where multiple broadphases are in close proximity.
        /// </summary>
        private float _broadphaseExpand;

        public override void Initialize()
        {
            base.Initialize();

            _logger = Logger.GetSawmill("physics");
            UpdatesOutsidePrediction = true;

            UpdatesAfter.Add(typeof(SharedTransformSystem));

            SubscribeLocalEvent<BroadphaseComponent, ComponentAdd>(OnBroadphaseAdd);
            SubscribeLocalEvent<GridAddEvent>(OnGridAdd);

            SubscribeLocalEvent<CollisionChangeEvent>(OnPhysicsUpdate);

            SubscribeLocalEvent<PhysicsComponent, MoveEvent>(OnMove);
            SubscribeLocalEvent<PhysicsComponent, RotateEvent>(OnRotate);

            var configManager = IoCManager.Resolve<IConfigurationManager>();
            configManager.OnValueChanged(CVars.BroadphaseExpand, SetBroadphaseExpand, true);

            SubscribeLocalEvent<MapChangedEvent>(ev =>
            {
                if (ev.Created)
                    OnMapCreated(ev);
                else
                    OnMapDestroyed(ev);
            });
        }

        private void SetBroadphaseExpand(float value) => _broadphaseExpand = value;

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            ProcessUpdates();
        }

        public override void FrameUpdate(float frameTime)
        {
            base.FrameUpdate(frameTime);
            ProcessUpdates();
        }

        /// <summary>
        /// Go through every deferred event and update the broadphase.
        /// </summary>
        public void ProcessUpdates()
        {
            // Unfortunately we can't re-use our broadphase transforms as controllers may update them.
            _physicsManager.ClearTransforms();
        }

        #region Find Contacts

        /// <summary>
        /// Check the AABB for each moved broadphase fixture and add any colliding entities to the movebuffer in case.
        /// </summary>
        private void FindGridContacts(
            MapId mapId,
            HashSet<IMapGrid> movedGrids,
            EntityQuery<PhysicsComponent> bodyQuery,
            EntityQuery<BroadphaseComponent> broadQuery)
        {
            // None moved this tick
            if (movedGrids.Count == 0) return;

            var mapBroadphase = broadQuery.GetComponent(_mapManager.GetMapEntityId(mapId));

            // This is so that if we're on a broadphase that's moving (e.g. a grid) we need to make sure anything
            // we move over is getting checked for collisions, and putting it on the movebuffer is the easiest way to do so.
            var moveBuffer = _moveBuffer[mapId];

            foreach (var grid in movedGrids)
            {
                DebugTools.Assert(grid.ParentMapId == mapId);
                var worldAABB = grid.WorldAABB;
                var enlargedAABB = worldAABB.Enlarged(_broadphaseExpand);

                var gridBody = bodyQuery.GetComponent(grid.GridEntityId);

                // TODO: Use the callback for this you ape.
                // Easier to just not go over each proxy as we already unioned the fixture's worldaabb.
                foreach (var other in mapBroadphase.Tree.QueryAabb(_queryBuffer, enlargedAABB))
                {
                    DebugTools.Assert(other.Fixture.Body != gridBody);
                    // 99% of the time it's just going to be the broadphase (for now the grid) itself.
                    // hence this body check makes this run significantly better.
                    // Also check if it's not already on the movebuffer.
                    if (moveBuffer.ContainsKey(other)) continue;

                    // To avoid updating during iteration.
                    // Don't need to transform as it's already in map terms.
                    _gridMoveBuffer[other] = other.AABB;
                }

                _queryBuffer.Clear();
            }

            foreach (var (proxy, worldAABB) in _gridMoveBuffer)
            {
                moveBuffer[proxy] = worldAABB;
            }
        }

        /// <summary>
        /// Go through every single created, moved, or touched proxy on the map and try to find any new contacts that should be created.
        /// </summary>
        internal void FindNewContacts(MapId mapId)
        {
            var moveBuffer = _moveBuffer[mapId];
            var movedGrids = _mapManager.GetMovedGrids(mapId);

            var broadphaseQuery = GetEntityQuery<BroadphaseComponent>();
            var physicsQuery = GetEntityQuery<PhysicsComponent>();
            var xformQuery = GetEntityQuery<TransformComponent>();

            // Find any entities being driven over that might need to be considered
            FindGridContacts(mapId, movedGrids, physicsQuery, broadphaseQuery);

            // There is some mariana trench levels of bullshit going on.
            // We essentially need to re-create Box2D's FindNewContacts but in a way that allows us to check every
            // broadphase intersecting a particular proxy instead of just on the 1 broadphase.
            // This means we can generate contacts across different broadphases.
            // If you have a better way of allowing for broadphases attached to grids then by all means code it yourself.

            // FindNewContacts is inherently going to be a lot slower than Box2D's normal version so we need
            // to cache a bunch of stuff to make up for it.
            var contactManager = Comp<SharedPhysicsMapComponent>(_mapManager.GetMapEntityIdOrThrow(mapId)).ContactManager;

            // Handle grids first as they're not stored on map broadphase at all.
            HandleGridCollisions(mapId, contactManager, movedGrids, physicsQuery, xformQuery);

            DebugTools.Assert(moveBuffer.Count > 0 || _pairBuffer.Count == 0);

            // TODO: Could store fixtures by broadphase for more perf?
            foreach (var (proxy, worldAABB) in moveBuffer)
            {
                var proxyBody = proxy.Fixture.Body;
                if (proxyBody.Deleted)
                {
                    // TODO: This happens in some grid deletion scenarios which does have an issue on github.
                    _logger.Error($"Deleted body {ToPrettyString(proxyBody.Owner)} made it to FindNewContacts; this should never happen!");
                    DebugTools.Assert(false);
                    continue;
                }

                _gridsPool.Clear();

                // Get every broadphase we may be intersecting.
                // Also TODO: Don't put grids on movebuffer so you get peak shuttle driving performance.
                foreach (var grid in _mapManager.FindGridsIntersecting(mapId, worldAABB.Enlarged(_broadphaseExpand), _gridsPool, xformQuery, physicsQuery))
                {
                    FindPairs(proxy, worldAABB, grid.GridEntityId, xformQuery, broadphaseQuery);
                }

                FindPairs(proxy, worldAABB, _mapManager.GetMapEntityId(mapId), xformQuery, broadphaseQuery);
            }

            foreach (var (proxyA, proxies) in _pairBuffer)
            {
                var proxyABody = proxyA.Fixture.Body;

                // TODO Why are we checking deleted what
                if (proxyABody.Deleted) continue;

                foreach (var other in proxies)
                {
                    var otherBody = other.Fixture.Body;

                    if (otherBody.Deleted) continue;

                    // Because we may be colliding with something asleep (due to the way grid movement works) need
                    // to make sure the contact doesn't fail.
                    // This is because we generate a contact across 2 different broadphases where both bodies aren't
                    // moving locally but are moving in world-terms.
                    if (proxyA.Fixture.Hard && other.Fixture.Hard &&
                        (_gridMoveBuffer.ContainsKey(proxyA) || _gridMoveBuffer.ContainsKey(other)))
                    {
                        proxyABody.WakeBody();
                        otherBody.WakeBody();
                    }

                    contactManager.AddPair(proxyA, other);
                }
            }

            movedGrids.Clear();
            _pairBuffer.Clear();
            moveBuffer.Clear();
            _gridMoveBuffer.Clear();
            _mapManager.ClearMovedGrids(mapId);
        }

        private void HandleGridCollisions(
            MapId mapId,
            ContactManager contactManager,
            HashSet<IMapGrid> movedGrids,
            EntityQuery<PhysicsComponent> bodyQuery,
            EntityQuery<TransformComponent> xformQuery)
        {
            foreach (var grid in movedGrids)
            {
                DebugTools.Assert(grid.ParentMapId == mapId);

                var mapGrid = (MapGrid)grid;
                var xform = xformQuery.GetComponent(grid.GridEntityId);

                var (worldPos, worldRot, worldMatrix, invWorldMatrix) = xform.GetWorldPositionRotationMatrixWithInv(xformQuery);

                var aabb = new Box2Rotated(grid.LocalAABB, worldRot).CalcBoundingBox().Translated(worldPos);

                // TODO: Need to handle grids colliding with non-grid entities with the same layer
                // (nothing in SS14 does this yet).

                var transform = bodyQuery.GetComponent(grid.GridEntityId).GetTransform(xform);
                _gridsPool.Clear();

                foreach (var colliding in _mapManager.FindGridsIntersecting(mapId, aabb, _gridsPool, xformQuery, bodyQuery, true))
                {
                    if (grid == colliding) continue;

                    var otherGrid = (MapGrid)colliding;
                    var otherGridBounds = colliding.WorldAABB;
                    var otherGridInvMatrix = colliding.InvWorldMatrix;
                    var otherTransform = bodyQuery.GetComponent(colliding.GridEntityId).GetTransform(xformQuery.GetComponent(colliding.GridEntityId));

                    // Get Grid2 AABB in grid1 ref
                    var aabb1 = grid.LocalAABB.Union(invWorldMatrix.TransformBox(otherGridBounds));

                    // TODO: AddPair has a nasty check in there that's O(n) but that's also a general physics problem.
                    var ourChunks = mapGrid.GetLocalMapChunks(aabb1);

                    // Only care about chunks on other grid overlapping us.
                    while (ourChunks.MoveNext(out var ourChunk))
                    {
                        var ourChunkWorld = worldMatrix.TransformBox(ourChunk.CachedBounds.Translated(ourChunk.Indices * grid.ChunkSize));
                        var ourChunkOtherRef = otherGridInvMatrix.TransformBox(ourChunkWorld);
                        var collidingChunks = otherGrid.GetLocalMapChunks(ourChunkOtherRef);

                        while (collidingChunks.MoveNext(out var collidingChunk))
                        {
                            foreach (var fixture in ourChunk.Fixtures)
                            {
                                for (var i = 0; i < fixture.Shape.ChildCount; i++)
                                {
                                    var fixAABB = fixture.Shape.ComputeAABB(transform, i);

                                    foreach (var otherFixture in collidingChunk.Fixtures)
                                    {
                                        for (var j = 0; j < otherFixture.Shape.ChildCount; j++)
                                        {
                                            var otherAABB = otherFixture.Shape.ComputeAABB(otherTransform, j);

                                            if (!fixAABB.Intersects(otherAABB)) continue;
                                            contactManager.AddPair(fixture, i, otherFixture, j, ContactFlags.Grid);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion

        private void FindPairs(
            FixtureProxy proxy,
            Box2 worldAABB,
            EntityUid broadphase,
            EntityQuery<TransformComponent> xformQuery,
            EntityQuery<BroadphaseComponent> broadphaseQuery)
        {
            DebugTools.Assert(proxy.Fixture.Body.CanCollide);

            var proxyBody = proxy.Fixture.Body;

            // Broadphase can't intersect with entities on itself so skip.
            if (proxyBody.Owner == broadphase) return;

            // Logger.DebugS("physics", $"Checking proxy for {proxy.Fixture.Body.Owner} on {broadphase.Owner}");
            Box2 aabb;
            var proxyBroad = proxyBody.Broadphase;

            if (proxyBroad == null)
            {
                _logger.Error($"Found null broadphase for {ToPrettyString(proxy.Fixture.Body.Owner)}");
                DebugTools.Assert(false);
                return;
            }

            // If it's the same broadphase as our body's one then don't need to translate the AABB.
            if (proxyBroad.Owner == broadphase)
            {
                aabb = proxy.AABB;
            }
            else
            {
                var broadXform = xformQuery.GetComponent(broadphase);
                aabb = broadXform.InvWorldMatrix.TransformBox(worldAABB);
            }

            var broadphaseComp = broadphaseQuery.GetComponent(broadphase);

            foreach (var other in broadphaseComp.Tree.QueryAabb(_queryBuffer, aabb))
            {
                DebugTools.Assert(other.Fixture.Body.CanCollide);
                // Logger.DebugS("physics", $"Checking {proxy.Fixture.Body.Owner} against {other.Fixture.Body.Owner} at {aabb}");

                // Do fast checks first and slower checks after (in ContactManager).
                if (proxy == other ||
                    proxy.Fixture.Body == other.Fixture.Body ||
                    !ContactManager.ShouldCollide(proxy.Fixture, other.Fixture)) continue;

                // Don't add duplicates.
                // Look it disgusts me but we can't do it Box2D's way because we're getting pairs
                // with different broadphases so can't use Proxy sorting to skip duplicates.
                // TODO: This needs to be better
                if (_pairBuffer.TryGetValue(other, out var existing) &&
                    existing.Contains(proxy))
                {
                    continue;
                }

                if (!_pairBuffer.TryGetValue(proxy, out var proxyExisting))
                {
                    proxyExisting = new HashSet<FixtureProxy>();
                    _pairBuffer[proxy] = proxyExisting;
                }

                proxyExisting.Add(other);
            }

            _queryBuffer.Clear();
        }

        /// <summary>
        /// If our broadphase has changed then remove us from our old one and add to our new one.
        /// </summary>
        internal void UpdateBroadphase(PhysicsComponent body, MapId oldMapId, TransformComponent? xform = null)
        {
            if (!Resolve(body.Owner, ref xform))
                return;

            var bodyQuery = GetEntityQuery<PhysicsComponent>();
            var fixturesQuery = GetEntityQuery<FixturesComponent>();
            var xformQuery = GetEntityQuery<TransformComponent>();
            var broadQuery = GetEntityQuery<BroadphaseComponent>();

            var newBroadphase = GetBroadphase(xform, broadQuery, xformQuery);
            TryGetMoveBuffer(oldMapId, out var oldMoveBuffer);

            RecursiveBroadphaseUpdate(xform, bodyQuery, fixturesQuery, xformQuery, broadQuery, newBroadphase, oldMoveBuffer);
        }

        private void RecursiveBroadphaseUpdate(
            TransformComponent xform,
            EntityQuery<PhysicsComponent> bodyQuery,
            EntityQuery<FixturesComponent> fixturesQuery,
            EntityQuery<TransformComponent> xformQuery,
            EntityQuery<BroadphaseComponent> broadQuery,
            BroadphaseComponent? newBroadphase,
            Dictionary<FixtureProxy, Box2>? oldMoveBuffer)
        {
            var uid = xform.Owner;
            var childEnumerator = xform.ChildEnumerator;

            if (bodyQuery.TryGetComponent(uid, out var body) &&
                body._canCollide &&
                fixturesQuery.TryGetComponent(uid, out var manager))
            {
                // TODO while iterating down through children, evaluate world position & rotation and pass into this function
                UpdateBodyBroadphase(body, manager, xform, newBroadphase, xformQuery, oldMoveBuffer);
            }

            if (xform.MapID != MapId.Nullspace && broadQuery.TryGetComponent(uid, out var parentBroad))
                newBroadphase = parentBroad;

            while (childEnumerator.MoveNext(out var child))
            {
                if (xformQuery.TryGetComponent(child, out var childXform))
                    RecursiveBroadphaseUpdate(childXform, bodyQuery, fixturesQuery, xformQuery, broadQuery, newBroadphase, oldMoveBuffer);
            }
        }

        internal void UpdateBodyBroadphase(
            PhysicsComponent body,
            FixturesComponent manager,
            TransformComponent xform,
            BroadphaseComponent? newBroadphase,
            EntityQuery<TransformComponent> xformQuery,
            Dictionary<FixtureProxy, Box2>? oldMoveBuffer)
        {
            if (body.Broadphase == newBroadphase)
                return;

            DestroyProxies(body, manager, oldMoveBuffer);
            body.Broadphase = newBroadphase;

            if (newBroadphase == null)
                return;

            // TODO optimize map moving. Seeing as we iterate downwards through children, world position/rotation can be
            // tracked, instead of re-calculated each time by iterating upwards though parents. But for deletions,
            // newBroadphase is null anyways, so this only matters for things like shuttles moving across maps.
            var (worldPos, worldRot) = _xformSys.GetWorldPositionRotation(xform, xformQuery);

            foreach (var (_, fixture) in manager.Fixtures)
            {
                // TODO pass in broadphaseXform
                CreateProxies(fixture, worldPos, worldRot);
            }
        }

        /// <summary>
        /// Remove all of our fixtures from the broadphase.
        /// </summary>
        private void DestroyProxies(PhysicsComponent body, FixturesComponent manager)
        {
            if (body.Broadphase == null)
                return;

            DestroyProxies(body, manager, _moveBuffer[Transform(body.Broadphase.Owner).MapID]);
        }

        private void DestroyProxies(PhysicsComponent body, FixturesComponent manager, Dictionary<FixtureProxy, Box2>? moveBuffer)
        {
            if (body.Broadphase == null)
                return;

            foreach (var (_, fixture) in manager.Fixtures)
            {
                var proxyCount = fixture.ProxyCount;
                for (var i = 0; i < proxyCount; i++)
                {
                    var proxy = fixture.Proxies[i];
                    body.Broadphase.Tree.RemoveProxy(proxy.ProxyId);
                    proxy.ProxyId = DynamicTree.Proxy.Free;
                    moveBuffer?.Remove(proxy);
                }

                fixture.ProxyCount = 0;
            }

            body.Broadphase = null;
        }

        private void OnPhysicsUpdate(ref CollisionChangeEvent ev)
        {
            var lifestage = ev.Body.LifeStage;

            // Oh god kill it with fire.
            if (lifestage is < ComponentLifeStage.Initialized or > ComponentLifeStage.Running) return;

            if (ev.CanCollide)
            {
                AddBody(ev.Body);
            }
            else
            {
                RemoveBody(ev.Body);
            }
        }

        public void AddBody(PhysicsComponent body, FixturesComponent? manager = null)
        {
            // TODO: Good idea? Ehhhhhhhhhhhh
            // The problem is there's some fuckery with events while an entity is initializing.
            // Can probably just bypass this by doing stuff in Update / FrameUpdate again but future problem
            // Also grids are special-cased due to their high fixture count.
            if (body.Broadphase != null ||
                _mapManager.IsGrid(body.Owner)) return;

            if (!Resolve(body.Owner, ref manager))
            {
                return;
            }

            // TODO: This should do an embed check... somehow... unfortunately we can't just awaken all pairs
            // because it makes stacks unstable...
            CreateProxies(body, manager);
        }

        internal void RemoveBody(PhysicsComponent body, FixturesComponent? manager = null)
        {
            // Not on any broadphase anyway.
            if (body.Broadphase == null) return;

            // TODO: Would reaaalllyy like for this to not be false in future
            if (!Resolve(body.Owner, ref manager, false))
            {
                return;
            }

            DestroyProxies(body, manager);
        }

        public void RegenerateContacts(PhysicsComponent body)
        {
            _physicsSystem.DestroyContacts(body);

            var broadphase = body.Broadphase;

            if (broadphase != null)
            {
                var mapId = EntityManager.GetComponent<TransformComponent>(body.Owner).MapID;

                foreach (var fixture in EntityManager.GetComponent<FixturesComponent>(body.Owner).Fixtures.Values)
                {
                    TouchProxies(mapId, broadphase, fixture);
                }
            }
        }

        public void Refilter(Fixture fixture)
        {
            // TODO: Call this method whenever collisionmask / collisionlayer changes
            // TODO: This should never becalled when body is null.
            DebugTools.Assert(fixture.Body != null);
            if (fixture.Body == null)
            {
                return;
            }

            var body = fixture.Body;

            foreach (var (_, contact) in fixture.Contacts)
            {
                contact.Flags |= ContactFlags.Filter;
            }

            var broadphase = body.Broadphase;

            // If nullspace or whatever ignore it.
            if (broadphase == null) return;

            TouchProxies(Transform(fixture.Body.Owner).MapID, broadphase, fixture);
        }

        private void TouchProxies(MapId mapId, BroadphaseComponent broadphase, Fixture fixture)
        {
            var broadphasePos = Transform(broadphase.Owner).WorldMatrix;

            foreach (var proxy in fixture.Proxies)
            {
                AddToMoveBuffer(mapId, proxy, broadphasePos.TransformBox(proxy.AABB));
            }
        }

        private void OnMove(EntityUid uid, PhysicsComponent component, ref MoveEvent args)
        {
            if (!component.CanCollide ||
                !TryComp<FixturesComponent>(uid, out var manager) ||
                _mapManager.IsGrid(uid)) return;

            var worldRot = Transform(uid).WorldRotation;

            SynchronizeFixtures(component, args.NewPosition.ToMapPos(EntityManager), (float) worldRot.Theta, manager);
        }

        private void OnRotate(EntityUid uid, PhysicsComponent component, ref RotateEvent args)
        {
            if (!component.CanCollide ||
                _mapManager.IsGrid(uid)) return;

            var xform = Transform(uid);
            var (worldPos, worldRot) = xform.GetWorldPositionRotation();
            DebugTools.Assert(xform.LocalRotation.Equals(args.NewRotation));

            SynchronizeFixtures(component, worldPos, (float) worldRot.Theta);
        }

        private void SynchronizeFixtures(PhysicsComponent body, Vector2 worldPos, float worldRot, FixturesComponent? manager = null)
        {
            if (!Resolve(body.Owner, ref manager))
            {
                return;
            }

            // Logger.DebugS("physics", $"Synchronizing fixtures for {body.Owner}");
            // Don't cache this as controllers may change it freely before we run physics!
            var xf = new Transform(worldPos, worldRot);

            if (body.Awake)
            {
                // TODO: SWEPT HERE
                // Check if we need to use the normal synchronize which also supports TOI
                // Otherwise, use the slightly faster one.

                // For now we'll just use the normal one as no TOI support
                foreach (var (_, fixture) in manager.Fixtures)
                {
                    if (fixture.ProxyCount == 0) continue;

                    // SynchronizezTOI(fixture, xf1, xf2);

                    Synchronize(fixture, xf);
                }
            }
            else
            {
                foreach (var (_, fixture) in manager.Fixtures)
                {
                    if (fixture.ProxyCount == 0) continue;

                    Synchronize(fixture, xf);
                }
            }
        }

        /// <summary>
        /// A more efficient Synchronize for 1 transform.
        /// </summary>
        private void Synchronize(Fixture fixture, Transform transform1)
        {
            // tl;dr update our bounding boxes stored in broadphase.
            var broadphase = fixture.Body.Broadphase!;
            var proxyCount = fixture.ProxyCount;

            var broadphaseXform = EntityManager.GetComponent<TransformComponent>(broadphase.Owner);

            var broadphaseMapId = broadphaseXform.MapID;
            var (broadphaseWorldPos, broadphaseWorldRot, broadphaseInvMatrix) = broadphaseXform.GetWorldPositionRotationInvMatrix();

            var relativePos1 = new Transform(
                broadphaseInvMatrix.Transform(transform1.Position),
                transform1.Quaternion2D.Angle - broadphaseWorldRot);

            for (var i = 0; i < proxyCount; i++)
            {
                var proxy = fixture.Proxies[i];
                var bounds = fixture.Shape.ComputeAABB(relativePos1, i);
                proxy.AABB = bounds;
                var displacement = Vector2.Zero;
                broadphase.Tree.MoveProxy(proxy.ProxyId, bounds, displacement);

                var worldAABB = new Box2Rotated(bounds, broadphaseWorldRot, Vector2.Zero)
                    .CalcBoundingBox()
                    .Translated(broadphaseWorldPos);

                AddToMoveBuffer(broadphaseMapId, proxy, worldAABB);
            }
        }

        private void AddToMoveBuffer(MapId mapId, FixtureProxy proxy, Box2 aabb)
        {
            if(mapId == MapId.Nullspace)
                return;

            DebugTools.Assert(proxy.Fixture.Body.CanCollide);

            _moveBuffer[mapId][proxy] = aabb;
        }

        // For use when recursively deleting an entity, to avoid may dictionary lookups
        internal bool TryGetMoveBuffer(MapId mapId, [NotNullWhen(true)]out Dictionary<FixtureProxy, Box2>? buffer)
            => _moveBuffer.TryGetValue(mapId, out buffer);

        internal void RemoveFromMoveBuffer(FixturesComponent manager, Dictionary<FixtureProxy, Box2> buffer)
        {
            foreach (var (_, fixture) in manager.Fixtures)
            {
                for (var i = 0; i < fixture.ProxyCount; i++)
                {
                    buffer.Remove(fixture.Proxies[i]);
                }
            }
        }

        /// <summary>
        /// Get broadphase proxies from the body's fixtures and add them to the relevant broadphase.
        /// </summary>
        /// <param name="useCache">Whether we should use cached broadphase data. This is only valid during the physics step.</param>
        private void CreateProxies(PhysicsComponent body, FixturesComponent? manager = null, TransformComponent? xform = null)
        {
            if (!Resolve(body.Owner, ref manager, ref xform) ||
                xform.MapID == MapId.Nullspace) return;

            var (worldPos, worldRot) = xform.GetWorldPositionRotation();

            // Outside of PVS (TODO Remove when PVS is better)
            if (float.IsNaN(worldPos.X) || float.IsNaN(worldPos.Y))
            {
                return;
            }

            var broadphase = GetBroadphase(xform);

            if (broadphase == null)
            {
                throw new InvalidBroadphaseException($"Unable to find broadphase for {body.Owner}");
            }

            if (body.Broadphase != null)
            {
                throw new InvalidBroadphaseException($"{body.Owner} already has proxies on a broadphase?");
            }

            body.Broadphase = broadphase;

            foreach (var (_, fixture) in manager.Fixtures)
            {
                CreateProxies(fixture, worldPos, worldRot);
            }
            // Logger.DebugS("physics", $"Created proxies for {body.Owner} on {broadphase.Owner}");
        }

        /// <summary>
        /// Create the proxies for this fixture on the body's broadphase.
        /// </summary>
        internal void CreateProxies(Fixture fixture, Vector2 worldPos, Angle worldRot)
        {
            // Ideally we would always just defer this until Update / FrameUpdate but that will have to wait for a future
            // PR for my own sanity.

            DebugTools.Assert(fixture.ProxyCount == 0);
            DebugTools.Assert(EntityManager.GetComponent<TransformComponent>(fixture.Body.Owner).MapID != MapId.Nullspace);

            var proxyCount = fixture.Shape.ChildCount;

            if (proxyCount == 0) return;

            var broadphase = fixture.Body.Broadphase;

            if (broadphase == null)
            {
                throw new InvalidBroadphaseException($"Unable to find broadphase for create on {fixture.Body.Owner}");
            }

            fixture.ProxyCount = proxyCount;
            var proxies = fixture.Proxies;

            Array.Resize(ref proxies, proxyCount);
            fixture.Proxies = proxies;

            var broadphaseXform = EntityManager.GetComponent<TransformComponent>(broadphase.Owner);

            var (broadphaseWorldPosition, broadphaseWorldRotation, broadphaseInvMatrix) = broadphaseXform.GetWorldPositionRotationInvMatrix();

            var localPos = broadphaseInvMatrix.Transform(worldPos);

            var transform = new Transform(localPos, worldRot - broadphaseWorldRotation);
            var mapId = broadphaseXform.MapID;

            for (var i = 0; i < proxyCount; i++)
            {
                var bounds = fixture.Shape.ComputeAABB(transform, i);
                var proxy = new FixtureProxy(bounds, fixture, i);
                DebugTools.Assert(fixture.Body.CanCollide);
                proxy.ProxyId = broadphase.Tree.AddProxy(ref proxy);
                fixture.Proxies[i] = proxy;

                var worldAABB = new Box2Rotated(bounds, broadphaseWorldRotation, Vector2.Zero)
                    .CalcBoundingBox()
                    .Translated(broadphaseWorldPosition);

                AddToMoveBuffer(mapId, proxy, worldAABB);
            }
        }

        /// <summary>
        /// Destroy the proxies for this fixture on the broadphase.
        /// </summary>
        internal void DestroyProxies(BroadphaseComponent broadphase, Fixture fixture, MapId map)
        {
            if (broadphase == null)
            {
                throw new InvalidBroadphaseException($"Unable to find broadphase for destroy on {fixture.Body}");
            }

            DebugTools.Assert(Transform(broadphase.Owner).MapID == map);

            var proxyCount = fixture.ProxyCount;
            var moveBuffer = _moveBuffer[map];

            for (var i = 0; i < proxyCount; i++)
            {
                var proxy = fixture.Proxies[i];
                broadphase.Tree.RemoveProxy(proxy.ProxyId);
                proxy.ProxyId = DynamicTree.Proxy.Free;
                moveBuffer.Remove(proxy);
            }

            fixture.ProxyCount = 0;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            var configManager = IoCManager.Resolve<IConfigurationManager>();
            configManager.UnsubValueChanged(CVars.BroadphaseExpand, SetBroadphaseExpand);
        }

        #region Broadphase management

        private void OnMapCreated(MapChangedEvent e)
        {
            if (e.Map == MapId.Nullspace) return;

            EntityManager.EnsureComponent<BroadphaseComponent>(_mapManager.GetMapEntityId(e.Map));
            _moveBuffer[e.Map] = new Dictionary<FixtureProxy, Box2>(64);
        }

        private void OnMapDestroyed(MapChangedEvent e)
        {
            _moveBuffer.Remove(e.Map);
        }

        private void OnGridAdd(GridAddEvent ev)
        {
            EntityManager.EnsureComponent<BroadphaseComponent>(ev.EntityUid);
        }

        private void OnBroadphaseAdd(EntityUid uid, BroadphaseComponent component, ComponentAdd args)
        {
            var capacity = (int) Math.Max(MinimumBroadphaseCapacity, Math.Ceiling(EntityManager.GetComponent<TransformComponent>(component.Owner).ChildCount / (float) MinimumBroadphaseCapacity) * MinimumBroadphaseCapacity);
            component.Tree = new DynamicTreeBroadPhase(capacity);
        }

        #endregion

        /// <summary>
        /// Attempt to get the relevant broadphase for this entity.
        /// Can return null if it's the map entity.
        /// </summary>
        private BroadphaseComponent? GetBroadphase(TransformComponent xform)
        {
            if (xform.MapID == MapId.Nullspace) return null;

            var broadQuery = GetEntityQuery<BroadphaseComponent>();
            var xformQuery = GetEntityQuery<TransformComponent>();
            return GetBroadphase(xform, broadQuery, xformQuery);
        }

        private BroadphaseComponent? GetBroadphase(TransformComponent xform, EntityQuery<BroadphaseComponent> broadQuery, EntityQuery<TransformComponent> xformQuery)
        {
            if (xform.MapID == MapId.Nullspace) return null;

            var parent = xform.ParentUid;

            // if it's map (or in null-space) return null. Grids should return the map's broadphase.

            while (parent.IsValid())
            {
                if (broadQuery.TryGetComponent(parent, out var comp)) return comp;
                parent = xformQuery.GetComponent(parent).ParentUid;
            }

            return null;
        }

        // TODO: The below is slow and should just query the map's broadphase directly. The problem is that
        // there's some ordering stuff going on where the broadphase has queued all of its updates but hasn't applied
        // them yet so this query will fail on initialization which chains into a whole lot of issues.
        internal IEnumerable<BroadphaseComponent> GetBroadphases(MapId mapId, Box2 aabb)
        {
            // TODO Okay so problem: If we just do Encloses that's a lot faster BUT it also means we don't return the
            // map's broadphase which avoids us iterating over it for 99% of bodies.

            if (mapId == MapId.Nullspace) yield break;

            foreach (var (broadphase, xform) in EntityManager.EntityQuery<BroadphaseComponent, TransformComponent>(true))
            {
                if (xform.MapID != mapId) continue;

                if (!EntityManager.TryGetComponent(broadphase.Owner, out IMapGridComponent? mapGrid))
                {
                    yield return broadphase;
                    continue;
                }

                var grid = (IMapGridInternal) _mapManager.GetGrid(mapGrid.GridIndex);

                // Won't worry about accurate bounds checks as it's probably slower in most use cases.
                var chunkEnumerator = grid.GetMapChunks(aabb);

                if (chunkEnumerator.MoveNext(out _))
                {
                    yield return broadphase;
                }
            }
        }

        private sealed class InvalidBroadphaseException : Exception
        {
            public InvalidBroadphaseException() {}

            public InvalidBroadphaseException(string message) : base(message) {}
        }
    }
}

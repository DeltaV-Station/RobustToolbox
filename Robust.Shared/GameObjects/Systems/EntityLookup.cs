using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Utility;

namespace Robust.Shared.GameObjects
{
    [Flags]
    public enum LookupFlags : byte
    {
        None = 0,
        Approximate = 1 << 0,
        IncludeAnchored = 1 << 1,
        // IncludeGrids = 1 << 2,
    }

    // TODO: Nuke IEntityLookup and just make a system
    public interface IEntityLookup
    {
        // Not an EntitySystem given _entityManager has a dependency on it which means it's just easier to IoC it for tests.

        void Startup();

        void Shutdown();

        bool AnyEntitiesIntersecting(MapId mapId, Box2 box, LookupFlags flags = LookupFlags.IncludeAnchored);

        IEnumerable<EntityUid> GetEntitiesInMap(MapId mapId, LookupFlags flags = LookupFlags.IncludeAnchored);

        IEnumerable<EntityUid> GetEntitiesAt(MapId mapId, Vector2 position, LookupFlags flags = LookupFlags.IncludeAnchored);

        IEnumerable<EntityUid> GetEntitiesInArc(EntityCoordinates coordinates, float range, Angle direction,
            float arcWidth, LookupFlags flags = LookupFlags.IncludeAnchored);

        IEnumerable<EntityUid> GetEntitiesIntersecting(GridId gridId, IEnumerable<Vector2i> gridIndices);

        IEnumerable<EntityUid> GetEntitiesIntersecting(GridId gridId, Vector2i gridIndices);

        IEnumerable<EntityUid> GetEntitiesIntersecting(TileRef tileRef);

        IEnumerable<EntityUid> GetEntitiesIntersecting(MapId mapId, Box2 worldAABB, LookupFlags flags = LookupFlags.IncludeAnchored);

        IEnumerable<EntityUid> GetEntitiesIntersecting(MapId mapId, Box2Rotated worldAABB, LookupFlags flags = LookupFlags.IncludeAnchored);

        IEnumerable<EntityUid> GetEntitiesIntersecting(EntityUid entity, float enlarged = 0f, LookupFlags flags = LookupFlags.IncludeAnchored);

        IEnumerable<EntityUid> GetEntitiesIntersecting(MapCoordinates position, LookupFlags flags = LookupFlags.IncludeAnchored);

        IEnumerable<EntityUid> GetEntitiesIntersecting(EntityCoordinates position, LookupFlags flags = LookupFlags.IncludeAnchored);

        IEnumerable<EntityUid> GetEntitiesIntersecting(MapId mapId, Vector2 position, LookupFlags flags = LookupFlags.IncludeAnchored);

        void FastEntitiesIntersecting(in MapId mapId, ref Box2 worldAABB, EntityUidQueryCallback callback, LookupFlags flags = LookupFlags.IncludeAnchored);

        void FastEntitiesIntersecting(EntityLookupComponent lookup, ref Box2 localAABB, EntityUidQueryCallback callback);

        IEnumerable<EntityUid> GetEntitiesInRange(EntityCoordinates position, float range, LookupFlags flags = LookupFlags.IncludeAnchored);

        IEnumerable<EntityUid> GetEntitiesInRange(EntityUid entity, float range, LookupFlags flags = LookupFlags.IncludeAnchored);

        IEnumerable<EntityUid> GetEntitiesInRange(MapId mapId, Vector2 point, float range, LookupFlags flags = LookupFlags.IncludeAnchored);

        IEnumerable<EntityUid> GetEntitiesInRange(MapId mapId, Box2 box, float range, LookupFlags flags = LookupFlags.IncludeAnchored);

        bool IsIntersecting(EntityUid entityOne, EntityUid entityTwo);

        Box2 GetWorldAabbFromEntity(in EntityUid ent, TransformComponent? xform = null);

        Box2 GetLocalBounds(TileRef tileRef, ushort tileSize);

        Box2 GetLocalBounds(Vector2i gridIndices, ushort tileSize);

        Box2Rotated GetWorldBounds(TileRef tileRef, Matrix3? worldMatrix = null, Angle? angle = null);
    }

    [UsedImplicitly]
    public sealed partial class EntityLookup : IEntityLookup, IEntityEventSubscriber
    {
        private readonly IEntityManager _entityManager;
        private readonly IMapManager _mapManager;
        private SharedContainerSystem _container = default!;
        private SharedTransformSystem _transform = default!;

        private const int GrowthRate = 256;

        private const float PointEnlargeRange = .00001f / 2;

        /// <summary>
        /// Like RenderTree we need to enlarge our lookup range for EntityLookupComponent as an entity is only ever on
        /// 1 EntityLookupComponent at a time (hence it may overlap without another lookup).
        /// </summary>
        private float _lookupEnlargementRange;

        // TODO: Should combine all of the methods that check for IPhysBody and just use the one GetWorldAabbFromEntity method

        // TODO: Combine GridTileLookupSystem and entity anchoring together someday.
        // Queries are a bit of spaghet rn but ideally you'd just have:
        // A) The fast tile-based one
        // B) The physics-only one (given physics needs it to be fast af)
        // C) A generic use one that covers anything not caught in the above.

        public bool Started { get; private set; } = false;

        public EntityLookup(IEntityManager entityManager, IMapManager mapManager)
        {
            _entityManager = entityManager;
            _mapManager = mapManager;
        }

        public void Startup()
        {
            if (Started)
            {
                throw new InvalidOperationException("Startup() called multiple times.");
            }

            _container = _entityManager.EntitySysManager.GetEntitySystem<SharedContainerSystem>();
            _transform = _entityManager.EntitySysManager.GetEntitySystem<SharedTransformSystem>();
            var configManager = IoCManager.Resolve<IConfigurationManager>();
            configManager.OnValueChanged(CVars.LookupEnlargementRange, value => _lookupEnlargementRange = value, true);

            var eventBus = _entityManager.EventBus;
            eventBus.SubscribeEvent<MoveEvent>(EventSource.Local, this, OnMove);
            eventBus.SubscribeEvent<RotateEvent>(EventSource.Local, this, OnRotate);
            eventBus.SubscribeEvent<EntParentChangedMessage>(EventSource.Local, this, OnParentChange);
            eventBus.SubscribeEvent<AnchorStateChangedEvent>(EventSource.Local, this, OnAnchored);

            eventBus.SubscribeLocalEvent<EntityLookupComponent, ComponentAdd>(OnLookupAdd);
            eventBus.SubscribeLocalEvent<EntityLookupComponent, ComponentShutdown>(OnLookupShutdown);
            eventBus.SubscribeEvent<GridInitializeEvent>(EventSource.Local, this, OnGridInit);

            eventBus.SubscribeEvent<EntityTerminatingEvent>(EventSource.Local, this, OnTerminate);

            _entityManager.EntityInitialized += OnEntityInit;
            _mapManager.MapCreated += OnMapCreated;
            Started = true;
        }

        public void Shutdown()
        {
            // If we haven't even started up, there's nothing to clean up then.
            if (!Started)
                return;

            _entityManager.EntityInitialized -= OnEntityInit;
            _mapManager.MapCreated -= OnMapCreated;
            Started = false;
        }

        private void OnAnchored(ref AnchorStateChangedEvent args)
        {
            // This event needs to be handled immediately as anchoring is handled immediately
            // and any callers may potentially get duplicate entities that just changed state.
            if (args.Anchored)
            {
                RemoveFromEntityTree(args.Entity);
            }
            else if (_entityManager.TryGetComponent(args.Entity, out MetaDataComponent? meta) && meta.EntityLifeStage < EntityLifeStage.Terminating)
            {
                var xformQuery = _entityManager.GetEntityQuery<TransformComponent>();
                var xform = xformQuery.GetComponent(args.Entity);
                var lookup = GetLookup(args.Entity, xform, xformQuery);

                if (lookup == null)
                    throw new InvalidOperationException();

                var lookupXform = xformQuery.GetComponent(lookup.Owner);
                var coordinates = _transform.GetMoverCoordinates(xform.Coordinates, xformQuery);
                DebugTools.Assert(coordinates.EntityId == lookup.Owner);

                // If we're contained then LocalRotation should be 0 anyway.
                var aabb = GetLookupAABB(args.Entity, coordinates.Position, xform.WorldRotation - lookupXform.WorldRotation, xform, xformQuery);
                AddToEntityTree(lookup, xform, aabb, xformQuery);
            }
            // else -> the entity is terminating. We can ignore this un-anchor event, as this entity will be removed by the tree via OnEntityDeleted.
        }

        #region DynamicTree

        private void OnLookupShutdown(EntityUid uid, EntityLookupComponent component, ComponentShutdown args)
        {
            component.Tree.Clear();
        }

        private void OnGridInit(GridInitializeEvent ev)
        {
            _entityManager.EnsureComponent<EntityLookupComponent>(ev.EntityUid);
        }

        private void OnLookupAdd(EntityUid uid, EntityLookupComponent component, ComponentAdd args)
        {
            int capacity;

            if (_entityManager.TryGetComponent(uid, out TransformComponent? xform))
            {
                capacity = (int) Math.Min(256, Math.Ceiling(xform.ChildCount / (float) GrowthRate) * GrowthRate);
            }
            else
            {
                capacity = 256;
            }

            component.Tree = new DynamicTree<EntityUid>(
                GetTreeAABB,
                capacity: capacity,
                growthFunc: x => x == GrowthRate ? GrowthRate * 8 : x * 2
            );
        }

        private void OnMapCreated(object? sender, MapEventArgs eventArgs)
        {
            if (eventArgs.Map == MapId.Nullspace) return;

            _entityManager.EnsureComponent<EntityLookupComponent>(_mapManager.GetMapEntityId(eventArgs.Map));
        }

        private Box2 GetTreeAABB(in EntityUid entity)
        {
            // TODO: Should feed in AABB to lookup so it's not enlarged unnecessarily
            var aabb = GetWorldAABB(entity);
            var xformQuery = _entityManager.GetEntityQuery<TransformComponent>();
            var tree = GetLookup(entity, xformQuery);

            if (tree == null)
                return aabb;

            return xformQuery.GetComponent(tree.Owner).InvWorldMatrix.TransformBox(aabb);
        }

        #endregion

        #region Entity events

        private void OnTerminate(ref EntityTerminatingEvent args)
        {
            RemoveFromEntityTree(args.Owner, false);
        }

        private void OnEntityInit(object? sender, EntityUid uid)
        {
            var xformQuery = _entityManager.GetEntityQuery<TransformComponent>();

            if (!xformQuery.TryGetComponent(uid, out var xform) ||
                xform.Anchored ||
                _mapManager.IsMap(uid) ||
                _mapManager.IsGrid(uid)) return;

            var lookup = GetLookup(uid, xform, xformQuery);

            // If nullspace or the likes.
            if (lookup == null) return;

            var lookupXform = xformQuery.GetComponent(lookup.Owner);
            var coordinates = _transform.GetMoverCoordinates(xform.Coordinates, xformQuery);
            DebugTools.Assert(coordinates.EntityId == lookup.Owner);

            // If we're contained then LocalRotation should be 0 anyway.
            var aabb = GetLookupAABB(uid, coordinates.Position, xform.WorldRotation - lookupXform.WorldRotation, xform, xformQuery);

            // Any child entities should be handled by their own OnEntityInit
            AddToEntityTree(lookup, xform, aabb, xformQuery, false);
        }

        private void OnMove(ref MoveEvent args)
        {
            UpdatePosition(args.Sender, args.Component);
        }

        private void OnRotate(ref RotateEvent args)
        {
            UpdatePosition(args.Sender, args.Component);
        }

        private void UpdatePosition(EntityUid uid, TransformComponent xform)
        {
            // Even if the entity is contained it may have children that aren't so we still need to update.
            if (!CanMoveUpdate(uid, xform)) return;

            var xformQuery = _entityManager.GetEntityQuery<TransformComponent>();
            var lookup = GetLookup(uid, xform, xformQuery);

            if (lookup == null) return;

            var lookupXform = xformQuery.GetComponent(lookup.Owner);
            var coordinates = _transform.GetMoverCoordinates(xform.Coordinates, xformQuery);
            var aabb = GetLookupAABB(uid, coordinates.Position, xform.WorldRotation - lookupXform.WorldRotation, xformQuery.GetComponent(uid), xformQuery);
            AddToEntityTree(lookup, xform, aabb, xformQuery);
        }

        private bool CanMoveUpdate(EntityUid uid, TransformComponent xform)
        {
            return !_mapManager.IsMap(uid) &&
                     !_mapManager.IsGrid(uid) &&
                     !_container.IsEntityInContainer(uid, xform);
        }

        private void OnParentChange(ref EntParentChangedMessage args)
        {
            if (_mapManager.IsMap(args.Entity) ||
                _mapManager.IsGrid(args.Entity) ||
                _entityManager.GetComponent<MetaDataComponent>(args.Entity).EntityLifeStage < EntityLifeStage.Initialized) return;

            EntityLookupComponent? oldLookup = null;
            var xformQuery = _entityManager.GetEntityQuery<TransformComponent>();
            var xform = xformQuery.GetComponent(args.Entity);

            if (args.OldParent != null)
            {
                oldLookup = GetLookup(args.OldParent.Value, xformQuery);
            }

            var newLookup = GetLookup(args.Entity, xform, xformQuery);

            // If parent is the same then no need to do anything as position should stay the same.
            if (oldLookup == newLookup) return;

            RemoveFromEntityTree(oldLookup, xform, xformQuery);

            if (newLookup != null)
                AddToEntityTree(newLookup, xform, xformQuery);
        }

        private void AddToEntityTree(
            EntityLookupComponent lookup,
            TransformComponent xform,
            EntityQuery<TransformComponent> xformQuery,
            bool recursive = true,
            bool contained = false)
        {
            var lookupXform = xformQuery.GetComponent(lookup.Owner);
            var coordinates = _transform.GetMoverCoordinates(xform.Coordinates, xformQuery);
            // If we're contained then LocalRotation should be 0 anyway.
            var aabb = GetLookupAABB(xform.Owner, coordinates.Position, xform.WorldRotation - lookupXform.WorldRotation, xform, xformQuery);
            AddToEntityTree(lookup, xform, aabb, xformQuery, recursive, contained);
        }

        private void AddToEntityTree(
            EntityLookupComponent? lookup,
            TransformComponent xform,
            Box2 aabb,
            EntityQuery<TransformComponent> xformQuery,
            bool recursive = true,
            bool contained = false)
        {
            // If entity is in nullspace then no point keeping track of data structure.
            if (lookup == null) return;

            if (!xform.Anchored)
                lookup.Tree.AddOrUpdate(xform.Owner, aabb);

            var childEnumerator = xform.ChildEnumerator;

            if (xform.ChildCount == 0 || !recursive) return;

            // TODO: Pass this down instead son.
            var lookupXform = xformQuery.GetComponent(lookup.Owner);
            // TODO: Just don't store contained stuff, it's way too expensive for updates and makes the tree much bigger.

            // Recursively update children.
            if (contained)
            {
                // Just re-use the topmost AABB.
                while (childEnumerator.MoveNext(out var child))
                {
                    AddToEntityTree(lookup, xformQuery.GetComponent(child.Value), aabb, xformQuery, contained: true);
                }
            }
            // If they're in a container then it just uses the parent's AABB.
            else if (_entityManager.TryGetComponent<ContainerManagerComponent>(xform.Owner, out var conManager))
            {
                while (childEnumerator.MoveNext(out var child))
                {
                    if (conManager.ContainsEntity(child.Value))
                    {
                        AddToEntityTree(lookup, xformQuery.GetComponent(child.Value), aabb, xformQuery, contained: true);
                    }
                    else
                    {
                        var coordinates = _transform.GetMoverCoordinates(xform.Coordinates, xformQuery);
                        var childXform = xformQuery.GetComponent(child.Value);
                        // TODO: If we have 0 position and not contained can optimise these further, but future problem.
                        var childAABB = GetLookupAABBNoContainer(child.Value, coordinates.Position, childXform.WorldRotation - lookupXform.WorldRotation);
                        AddToEntityTree(lookup, childXform, childAABB, xformQuery);
                    }
                }
            }
            else
            {
                while (childEnumerator.MoveNext(out var child))
                {
                    var coordinates = _transform.GetMoverCoordinates(xform.Coordinates, xformQuery);
                    var childXform = xformQuery.GetComponent(child.Value);
                    // TODO: If we have 0 position and not contained can optimise these further, but future problem.
                    var childAABB = GetLookupAABBNoContainer(child.Value, coordinates.Position, childXform.WorldRotation - lookupXform.WorldRotation);
                    AddToEntityTree(lookup, childXform, childAABB, xformQuery);
                }
            }
        }

        private void RemoveFromEntityTree(EntityUid uid, bool recursive = true)
        {
            var xformQuery = _entityManager.GetEntityQuery<TransformComponent>();
            var xform = xformQuery.GetComponent(uid);
            var lookup = GetLookup(uid, xform, xformQuery);
            RemoveFromEntityTree(lookup, xform, xformQuery, recursive);
        }

        /// <summary>
        /// Recursively iterates through this entity's children and removes them from the entitylookupcomponent.
        /// </summary>
        private void RemoveFromEntityTree(EntityLookupComponent? lookup, TransformComponent xform, EntityQuery<TransformComponent> xformQuery, bool recursive = true)
        {
            // TODO: Move this out of the loop
            if (lookup == null) return;

            lookup.Tree.Remove(xform.Owner);

            if (!recursive) return;

            var childEnumerator = xform.ChildEnumerator;

            while (childEnumerator.MoveNext(out var child))
            {
                RemoveFromEntityTree(lookup, xformQuery.GetComponent(child.Value), xformQuery);
            }
        }

        #endregion

        #region Spatial Queries

        // TODO: Need to nuke / move the below to queries.

        private LookupsEnumerator GetLookupsIntersecting(MapId mapId, Box2 worldAABB)
        {
            _mapManager.FindGridsIntersectingEnumerator(mapId, worldAABB, out var gridEnumerator, true);

            return new LookupsEnumerator(_entityManager, _mapManager, mapId, gridEnumerator);
        }

        private struct LookupsEnumerator
        {
            private IEntityManager _entityManager;
            private IMapManager _mapManager;

            private MapId _mapId;
            private FindGridsEnumerator _enumerator;

            private bool _final;

            public LookupsEnumerator(IEntityManager entityManager, IMapManager mapManager, MapId mapId, FindGridsEnumerator enumerator)
            {
                _entityManager = entityManager;
                _mapManager = mapManager;

                _mapId = mapId;
                _enumerator = enumerator;
                _final = false;
            }

            public bool MoveNext([NotNullWhen(true)] out EntityLookupComponent? component)
            {
                if (!_enumerator.MoveNext(out var grid))
                {
                    if (_final || _mapId == MapId.Nullspace)
                    {
                        component = null;
                        return false;
                    }

                    _final = true;
                    EntityUid mapUid = _mapManager.GetMapEntityIdOrThrow(_mapId);
                    component = _entityManager.GetComponent<EntityLookupComponent>(mapUid);
                    return true;
                }

                // TODO: Recursive and all that.
                component = _entityManager.GetComponent<EntityLookupComponent>(grid.GridEntityId);
                return true;
            }
        }

        private IEnumerable<EntityUid> GetAnchored(MapId mapId, Box2 worldAABB, LookupFlags flags)
        {
            if ((flags & LookupFlags.IncludeAnchored) == 0x0) yield break;
            foreach (var grid in _mapManager.FindGridsIntersecting(mapId, worldAABB))
            {
                foreach (var uid in grid.GetAnchoredEntities(worldAABB))
                {
                    if (!_entityManager.EntityExists(uid)) continue;
                    yield return uid;
                }
            }
        }

        private IEnumerable<EntityUid> GetAnchored(MapId mapId, Box2Rotated worldBounds, LookupFlags flags)
        {
            if ((flags & LookupFlags.IncludeAnchored) == 0x0) yield break;
            foreach (var grid in _mapManager.FindGridsIntersecting(mapId, worldBounds))
            {
                foreach (var uid in grid.GetAnchoredEntities(worldBounds))
                {
                    if (!_entityManager.EntityExists(uid)) continue;
                    yield return uid;
                }
            }
        }

        /// <inheritdoc />
        public bool AnyEntitiesIntersecting(MapId mapId, Box2 box, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            var found = false;
            var enumerator = GetLookupsIntersecting(mapId, box);

            while (enumerator.MoveNext(out var lookup))
            {
                var offsetBox = _entityManager.GetComponent<TransformComponent>(lookup.Owner).InvWorldMatrix.TransformBox(box);

                lookup.Tree.QueryAabb(ref found, (ref bool found, in EntityUid ent) =>
                {
                    if (_entityManager.Deleted(ent))
                        return true;

                    found = true;
                    return false;

                }, offsetBox, (flags & LookupFlags.Approximate) != 0x0);
            }

            if (!found)
            {
                foreach (var _ in GetAnchored(mapId, box, flags))
                {
                    return true;
                }
            }

            return found;
        }

        /// <inheritdoc />
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void FastEntitiesIntersecting(in MapId mapId, ref Box2 worldAABB, EntityUidQueryCallback callback, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            var enumerator = GetLookupsIntersecting(mapId, worldAABB);
            while (enumerator.MoveNext(out var lookup))
            {
                var offsetBox = _entityManager.GetComponent<TransformComponent>(lookup.Owner).InvWorldMatrix.TransformBox(worldAABB);

                lookup.Tree._b2Tree.FastQuery(ref offsetBox, (ref EntityUid data) => callback(data));
            }

            if ((flags & LookupFlags.IncludeAnchored) != 0x0)
            {
                foreach (var grid in _mapManager.FindGridsIntersecting(mapId, worldAABB))
                {
                    foreach (var uid in grid.GetAnchoredEntities(worldAABB))
                    {
                        if (!_entityManager.EntityExists(uid)) continue;
                        callback(uid);
                    }
                }
            }
        }

        /// <inheritdoc />
        public void FastEntitiesIntersecting(EntityLookupComponent lookup, ref Box2 localAABB, EntityUidQueryCallback callback)
        {
            lookup.Tree._b2Tree.FastQuery(ref localAABB, (ref EntityUid data) => callback(data));
        }

        /// <inheritdoc />
        public IEnumerable<EntityUid> GetEntitiesIntersecting(MapId mapId, Box2 worldAABB, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            if (mapId == MapId.Nullspace) return Enumerable.Empty<EntityUid>();

            var list = new List<EntityUid>();
            var enumerator = GetLookupsIntersecting(mapId, worldAABB);

            while (enumerator.MoveNext(out var lookup))
            {
                var offsetBox = _entityManager.GetComponent<TransformComponent>(lookup.Owner).InvWorldMatrix.TransformBox(worldAABB);

                lookup.Tree.QueryAabb(ref list, (ref List<EntityUid> list, in EntityUid ent) =>
                {
                    if (!_entityManager.Deleted(ent))
                    {
                        list.Add(ent);
                    }
                    return true;
                }, offsetBox, (flags & LookupFlags.Approximate) != 0x0);
            }

            foreach (var ent in GetAnchored(mapId, worldAABB, flags))
            {
                list.Add(ent);
            }

            return list;
        }

        /// <inheritdoc />
        public IEnumerable<EntityUid> GetEntitiesIntersecting(MapId mapId, Box2Rotated worldBounds, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            if (mapId == MapId.Nullspace) return Enumerable.Empty<EntityUid>();

            var list = new List<EntityUid>();
            var worldAABB = worldBounds.CalcBoundingBox();
            var enumerator = GetLookupsIntersecting(mapId, worldAABB);

            while (enumerator.MoveNext(out var lookup))
            {
                var offsetBox = _entityManager.GetComponent<TransformComponent>(lookup.Owner).InvWorldMatrix.TransformBox(worldBounds);

                lookup.Tree.QueryAabb(ref list, (ref List<EntityUid> list, in EntityUid ent) =>
                {
                    if (!_entityManager.Deleted(ent))
                    {
                        list.Add(ent);
                    }
                    return true;
                }, offsetBox, (flags & LookupFlags.Approximate) != 0x0);
            }

            foreach (var ent in GetAnchored(mapId, worldBounds, flags))
            {
                list.Add(ent);
            }

            return list;
        }

        /// <inheritdoc />
        public IEnumerable<EntityUid> GetEntitiesIntersecting(MapId mapId, Vector2 position, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            if (mapId == MapId.Nullspace) return Enumerable.Empty<EntityUid>();

            var aabb = new Box2(position, position).Enlarged(PointEnlargeRange);
            var list = new List<EntityUid>();
            var state = (list, position);

            var enumerator = GetLookupsIntersecting(mapId, aabb);

            while (enumerator.MoveNext(out var lookup))
            {
                var localPoint = _entityManager.GetComponent<TransformComponent>(lookup.Owner).InvWorldMatrix.Transform(position);

                lookup.Tree.QueryPoint(ref state, (ref (List<EntityUid> list, Vector2 position) state, in EntityUid ent) =>
                {
                    if (Intersecting(ent, state.position))
                    {
                        state.list.Add(ent);
                    }
                    return true;
                }, localPoint, (flags & LookupFlags.Approximate) != 0x0);
            }

            if ((flags & LookupFlags.IncludeAnchored) != 0x0 &&
                _mapManager.TryFindGridAt(mapId, position, out var grid) &&
                grid.TryGetTileRef(position, out var tile))
            {
                foreach (var uid in grid.GetAnchoredEntities(tile.GridIndices))
                {
                    if (!_entityManager.EntityExists(uid)) continue;
                    state.list.Add(uid);
                }
            }

            return list;
        }

        /// <inheritdoc />
        public IEnumerable<EntityUid> GetEntitiesIntersecting(MapCoordinates position, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            return GetEntitiesIntersecting(position.MapId, position.Position, flags);
        }

        /// <inheritdoc />
        public IEnumerable<EntityUid> GetEntitiesIntersecting(EntityCoordinates position, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            var mapPos = position.ToMap(_entityManager);
            return GetEntitiesIntersecting(mapPos.MapId, mapPos.Position, flags);
        }

        /// <inheritdoc />
        public IEnumerable<EntityUid> GetEntitiesIntersecting(EntityUid entity, float enlarged = 0f, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            var worldAABB = GetWorldAabbFromEntity(entity);
            var xform = _entityManager.GetComponent<TransformComponent>(entity);

            var (worldPos, worldRot) = xform.GetWorldPositionRotation();

            var enumerator = GetLookupsIntersecting(xform.MapID, worldAABB);
            var list = new List<EntityUid>();

            while (enumerator.MoveNext(out var lookup))
            {
                // To get the tightest bounds possible we'll re-calculate it for each lookup.
                var localBounds = GetLookupBounds(entity, lookup, worldPos, worldRot, enlarged);

                lookup.Tree.QueryAabb(ref list, (ref List<EntityUid> list, in EntityUid ent) =>
                {
                    if (!_entityManager.Deleted(ent))
                    {
                        list.Add(ent);
                    }
                    return true;
                }, localBounds, (flags & LookupFlags.Approximate) != 0x0);
            }

            foreach (var ent in GetAnchored(xform.MapID, worldAABB, flags))
            {
                list.Add(ent);
            }

            return list;
        }

        private Box2 GetLookupBounds(EntityUid uid, EntityLookupComponent lookup, Vector2 worldPos, Angle worldRot, float enlarged)
        {
            var (_, lookupRot, lookupInvWorldMatrix) = _entityManager.GetComponent<TransformComponent>(lookup.Owner).GetWorldPositionRotationInvMatrix();

            var localPos = lookupInvWorldMatrix.Transform(worldPos);
            var localRot = worldRot - lookupRot;

            if (_entityManager.TryGetComponent(uid, out FixturesComponent? manager))
            {
                var transform = new Transform(localPos, localRot);
                Box2? aabb = null;

                foreach (var (_, fixture) in manager.Fixtures)
                {
                    if (!fixture.Hard) continue;
                    for (var i = 0; i < fixture.Shape.ChildCount; i++)
                    {
                        aabb = aabb?.Union(fixture.Shape.ComputeAABB(transform, i)) ?? fixture.Shape.ComputeAABB(transform, i);
                    }
                }

                if (aabb != null)
                {
                    return aabb.Value.Enlarged(enlarged);
                }
            }

            // So IsEmpty checks don't get triggered
            return new Box2(localPos - float.Epsilon, localPos + float.Epsilon);
        }

        /// <inheritdoc />
        public bool IsIntersecting(EntityUid entityOne, EntityUid entityTwo)
        {
            var position = _entityManager.GetComponent<TransformComponent>(entityOne).MapPosition.Position;
            return Intersecting(entityTwo, position);
        }

        private bool Intersecting(EntityUid entity, Vector2 mapPosition)
        {
            if (_entityManager.TryGetComponent(entity, out IPhysBody? component))
            {
                if (component.GetWorldAABB().Contains(mapPosition))
                    return true;
            }
            else
            {
                var transform = _entityManager.GetComponent<TransformComponent>(entity);
                var entPos = transform.WorldPosition;
                if (MathHelper.CloseToPercent(entPos.X, mapPosition.X)
                    && MathHelper.CloseToPercent(entPos.Y, mapPosition.Y))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public IEnumerable<EntityUid> GetEntitiesInRange(EntityCoordinates position, float range, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            var mapCoordinates = position.ToMap(_entityManager);
            var mapPosition = mapCoordinates.Position;
            var aabb = new Box2(mapPosition - new Vector2(range, range),
                mapPosition + new Vector2(range, range));
            return GetEntitiesIntersecting(mapCoordinates.MapId, aabb, flags);
            // TODO: Use a circle shape here mate
        }

        /// <inheritdoc />
        public IEnumerable<EntityUid> GetEntitiesInRange(MapId mapId, Box2 box, float range, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            var aabb = box.Enlarged(range);
            return GetEntitiesIntersecting(mapId, aabb, flags);
        }

        /// <inheritdoc />
        public IEnumerable<EntityUid> GetEntitiesInRange(MapId mapId, Vector2 point, float range, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            var aabb = new Box2(point, point).Enlarged(range);
            return GetEntitiesIntersecting(mapId, aabb, flags);
        }

        /// <inheritdoc />
        public IEnumerable<EntityUid> GetEntitiesInRange(EntityUid entity, float range, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            var worldAABB = GetWorldAabbFromEntity(entity);
            return GetEntitiesInRange(_entityManager.GetComponent<TransformComponent>(entity).MapID, worldAABB, range, flags);
        }

        /// <inheritdoc />
        public IEnumerable<EntityUid> GetEntitiesInArc(EntityCoordinates coordinates, float range, Angle direction,
            float arcWidth, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            var position = coordinates.ToMap(_entityManager).Position;

            foreach (var entity in GetEntitiesInRange(coordinates, range * 2, flags))
            {
                var angle = new Angle(_entityManager.GetComponent<TransformComponent>(entity).WorldPosition - position);
                if (angle.Degrees < direction.Degrees + arcWidth / 2 &&
                    angle.Degrees > direction.Degrees - arcWidth / 2)
                    yield return entity;
            }
        }

        /// <inheritdoc />
        public IEnumerable<EntityUid> GetEntitiesInMap(MapId mapId, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            DebugTools.Assert((flags & LookupFlags.Approximate) == 0x0);

            foreach (EntityLookupComponent comp in _entityManager.EntityQuery<EntityLookupComponent>(true))
            {
                if (_entityManager.GetComponent<TransformComponent>(comp.Owner).MapID != mapId) continue;

                foreach (var entity in comp.Tree)
                {
                    if (_entityManager.Deleted(entity)) continue;

                    yield return entity;
                }
            }

            if ((flags & LookupFlags.IncludeAnchored) == 0x0) yield break;

            foreach (var grid in _mapManager.GetAllMapGrids(mapId))
            {
                foreach (var tile in grid.GetAllTiles())
                {
                    foreach (var uid in grid.GetAnchoredEntities(tile.GridIndices))
                    {
                        if (!_entityManager.EntityExists(uid)) continue;
                        yield return uid;
                    }
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<EntityUid> GetEntitiesAt(MapId mapId, Vector2 position, LookupFlags flags = LookupFlags.IncludeAnchored)
        {
            if (mapId == MapId.Nullspace) return Enumerable.Empty<EntityUid>();

            var list = new List<EntityUid>();

            var state = (list, position);

            var aabb = new Box2(position, position).Enlarged(PointEnlargeRange);
            var enumerator = GetLookupsIntersecting(mapId, aabb);

            while (enumerator.MoveNext(out var lookup))
            {
                var offsetPos = _entityManager.GetComponent<TransformComponent>(lookup.Owner).InvWorldMatrix.Transform(position);

                lookup.Tree.QueryPoint(ref state, (ref (List<EntityUid> list, Vector2 position) state, in EntityUid ent) =>
                {
                    state.list.Add(ent);
                    return true;
                }, offsetPos, (flags & LookupFlags.Approximate) != 0x0);
            }

            if ((flags & LookupFlags.IncludeAnchored) != 0x0)
            {
                foreach (var grid in _mapManager.FindGridsIntersecting(mapId, aabb))
                {
                    foreach (var uid in grid.GetAnchoredEntities(aabb))
                    {
                        if (!_entityManager.EntityExists(uid)) continue;
                        list.Add(uid);
                    }
                }
            }

            return list;
        }

        #endregion

        #region Entity DynamicTree

        private EntityLookupComponent? GetLookup(EntityUid entity, EntityQuery<TransformComponent> xformQuery)
        {
            var xform = xformQuery.GetComponent(entity);
            return GetLookup(entity, xform, xformQuery);
        }

        private EntityLookupComponent? GetLookup(EntityUid uid, TransformComponent xform, EntityQuery<TransformComponent> xformQuery)
        {
            if (xform.MapID == MapId.Nullspace)
                return null;

            var parent = xform.ParentUid;
            var lookupQuery = _entityManager.GetEntityQuery<EntityLookupComponent>();

            // If we're querying a map / grid just return it directly.
            if (lookupQuery.TryGetComponent(uid, out var lookup))
            {
                return lookup;
            }

            while (parent.IsValid())
            {
                if (lookupQuery.TryGetComponent(parent, out var comp)) return comp;
                parent = xformQuery.GetComponent(parent).ParentUid;
            }

            return null;
        }

        public Box2 GetWorldAabbFromEntity(in EntityUid ent, TransformComponent? xform = null)
        {
            return GetWorldAABB(ent, xform);
        }

        private Box2 GetWorldAABB(in EntityUid ent, TransformComponent? xform = null)
        {
            Vector2 pos;
            xform ??= _entityManager.GetComponent<TransformComponent>(ent);

            if ((!_entityManager.EntityExists(ent) ? EntityLifeStage.Deleted : _entityManager.GetComponent<MetaDataComponent>(ent).EntityLifeStage) >= EntityLifeStage.Deleted)
            {
                pos = xform.WorldPosition;
                return new Box2(pos, pos);
            }

            // MOCKS WHY
            if (ent.TryGetContainer(out var container, _entityManager))
            {
                return GetWorldAABB(container.Owner);
            }

            pos = xform.WorldPosition;

            return _entityManager.TryGetComponent(ent, out ILookupWorldBox2Component? lookup) ?
                lookup.GetWorldAABB(pos) :
                new Box2(pos, pos);
        }

        private Box2 GetLookupAABB(EntityUid uid, Vector2 position, Angle angle, TransformComponent xform, EntityQuery<TransformComponent> xformQuery)
        {
            // If we're in a container then we just use the container's bounds.
            if (_container.TryGetOuterContainer(uid, xform, out var container))
            {
                return GetLookupAABBNoContainer(container.Owner, position, angle);
            }

            return GetLookupAABBNoContainer(uid, position, angle);
        }

        /// <summary>
        /// Get the AABB of an entity relative to a <see cref="EntityLookupComponent"/>
        /// </summary>
        private Box2 GetLookupAABBNoContainer(EntityUid uid, Vector2 position, Angle angle)
        {
            // DebugTools.Assert(!_container.IsEntityInContainer(uid, xform));
            Box2 localAABB;
            var transform = new Transform(position, angle);

            if (_entityManager.TryGetComponent<ILookupWorldBox2Component>(uid, out var worldLookup))
            {
                localAABB = worldLookup.GetAABB(transform);
            }
            else
            {
                localAABB = new Box2Rotated(new Box2(transform.Position, transform.Position), transform.Quaternion2D.Angle, transform.Position).CalcBoundingBox();
            }

            return localAABB;
        }

        #endregion
    }
}

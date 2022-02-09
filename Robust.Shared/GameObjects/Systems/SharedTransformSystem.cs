using System.Collections.Generic;
using System.Linq;
using Robust.Shared.Containers;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Robust.Shared.GameObjects
{
    public abstract class SharedTransformSystem : EntitySystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly IEntityLookup _entityLookup = default!;

        private readonly Queue<MoveEvent> _gridMoves = new();
        private readonly Queue<MoveEvent> _otherMoves = new();

        /// <summary>
        /// Used by physics to avoid duplicate entity moves going out.
        /// </summary>
        internal bool EnableSubscriptions = true;

        public override void Initialize()
        {
            base.Initialize();

            UpdatesOutsidePrediction = true;

            _mapManager.TileChanged += MapManagerOnTileChanged;
            SubscribeLocalEvent<MoveEvent>(OnMoveEvent);
            SubscribeLocalEvent<RotateEvent>(OnRotateEvent);
        }

        // TODO: To avoid dupe updates going out should special-case this in physics.

        private void OnMoveEvent(ref MoveEvent ev)
        {
            if (!EnableSubscriptions ||
                !ev.NewPosition.IsValid(EntityManager)) return;

            OnUpdate(ev.Sender, ev.Component);
        }

        private void OnRotateEvent(ref RotateEvent ev)
        {
            if (!EnableSubscriptions) return;

            OnUpdate(ev.Sender, ev.Component);
        }

        /// <summary>
        /// Issues entity move events, excluding maps and grids.
        /// </summary>
        internal void OnUpdate(EntityUid uid, TransformComponent xform)
        {
            if (_mapManager.IsGrid(uid) || _mapManager.IsMap(uid)) return;

            // Client can issue nullspace events due to PVS so can't just assert it.
            // DebugTools.Assert(ev.Component.MapID != MapId.Nullspace);

            var moverCoordinates = GetMoverCoordinates(xform);
            var gridId = moverCoordinates.GetGridId(EntityManager);
            var mapId = moverCoordinates.ToMap(EntityManager).MapId;
            var localAABB = _entityLookup.GetLocalAABB(uid, xform);

            var moveEvent = new EntityMoveEvent(
                uid,
                moverCoordinates,
                xform,
                mapId,
                gridId,
                localAABB);

            RaiseLocalEvent(uid, ref moveEvent);

            // Check children on this one to avoid getting the query unnecessarily
            if (xform.ChildCount == 0) return;

            var xformQuery = EntityManager.GetEntityQuery<TransformComponent>();
            var childEnumerator = xform.ChildEnumerator;

            while (childEnumerator.MoveNext(out var child))
            {
                ChildMove(child.Value, xformQuery, ref moverCoordinates, mapId, gridId, localAABB);
            }
        }

        private void ChildMove(EntityUid uid, EntityQuery<TransformComponent> xformQuery, ref EntityCoordinates moverCoordinates, MapId mapId, GridId gridId, Box2 moverAABB)
        {
            var xform = xformQuery.GetComponent(uid);

            // If our localpos is 0 then we can re-use our parent's position
            // otherwise, recalculate
            if (!xform.LocalPosition.Equals(Vector2.Zero))
            {
                moverCoordinates = GetMoverCoordinates(xform);
            }

            var moveEvent = new EntityMoveEvent(
                uid,
                moverCoordinates,
                xform,
                mapId,
                gridId,
                moverAABB);

            RaiseLocalEvent(uid, ref moveEvent);

            var childEnumerator = xform.ChildEnumerator;

            while (childEnumerator.MoveNext(out var child))
            {
                ChildMove(child.Value, xformQuery, ref moverCoordinates, mapId, gridId, moverAABB);
            }
        }

        public EntityCoordinates GetMoverCoordinates(TransformComponent xform)
        {
            // If they're parented directly to the map or grid then just return the coordinates.
            if (!_mapManager.TryGetGrid(xform.GridID, out var grid))
            {
                var mapUid = _mapManager.GetMapEntityId(xform.MapID);
                var coordinates = xform.Coordinates;

                // Parented directly to the map.
                if (xform.ParentUid == mapUid)
                    return coordinates;

                return new EntityCoordinates(mapUid, coordinates.ToMapPos(EntityManager));
            }

            // Parented directly to the grid
            if (grid.GridEntityId == xform.ParentUid)
                return xform.Coordinates;

            // Parented to grid so convert their pos back to the grid.
            var gridPos = Transform(grid.GridEntityId).InvWorldMatrix.Transform(xform.WorldPosition);
            return new EntityCoordinates(grid.GridEntityId, gridPos);
        }

        public override void Shutdown()
        {
            _mapManager.TileChanged -= MapManagerOnTileChanged;
            base.Shutdown();
        }

        private void MapManagerOnTileChanged(object? sender, TileChangedEventArgs e)
        {
            if(e.NewTile.Tile != Tile.Empty)
                return;

            DeparentAllEntsOnTile(e.NewTile.GridIndex, e.NewTile.GridIndices);
        }

        /// <summary>
        ///     De-parents and unanchors all entities on a grid-tile.
        /// </summary>
        /// <remarks>
        ///     Used when a tile on a grid is removed (becomes space). Only de-parents entities if they are actually
        ///     parented to that grid. No more disemboweling mobs.
        /// </remarks>
        private void DeparentAllEntsOnTile(GridId gridId, Vector2i tileIndices)
        {
            var grid = _mapManager.GetGrid(gridId);
            var gridUid = grid.GridEntityId;
            var mapTransform = Transform(_mapManager.GetMapEntityId(grid.ParentMapId));
            var aabb = _entityLookup.GetLocalBounds(tileIndices, grid.TileSize);

            foreach (var entity in _entityLookup.GetEntitiesIntersecting(gridId, tileIndices).ToList())
            {
                // If a tile is being removed due to an explosion or somesuch, some entities are likely being deleted.
                // Avoid unnecessary entity updates.
                if (EntityManager.IsQueuedForDeletion(entity))
                    continue;

                var transform = Transform(entity);
                if (transform.ParentUid == gridUid && aabb.Contains(transform.LocalPosition))
                    transform.AttachParent(mapTransform);
            }
        }

        public void DeferMoveEvent(ref MoveEvent moveEvent)
        {
            if (EntityManager.HasComponent<IMapGridComponent>(moveEvent.Sender))
                _gridMoves.Enqueue(moveEvent);
            else
                _otherMoves.Enqueue(moveEvent);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            // Process grid moves first.
            Process(_gridMoves);
            Process(_otherMoves);

            void Process(Queue<MoveEvent> queue)
            {
                while (queue.TryDequeue(out var ev))
                {
                    if (EntityManager.Deleted(ev.Sender))
                    {
                        continue;
                    }

                    // Hopefully we can remove this when PVS gets updated to not use NaNs
                    if (!ev.NewPosition.IsValid(EntityManager))
                    {
                        continue;
                    }

                    RaiseLocalEvent(ev.Sender, ref ev);
                }
            }
        }
    }

    [ByRefEvent]
    public struct TransformInitializedEvent
    {
        public TransformComponent Component;
    }

    /// <summary>
    /// Raised whenever a non-map / non-grid entity moves.
    /// This is useful for tree structures based around those components that need it for updating.
    /// This is also raised on the children as well.
    /// </summary>
    [ByRefEvent]
    public readonly struct EntityMoveEvent
    {
        /// <summary>
        /// The moving entity.
        /// </summary>
        public readonly EntityUid Entity;

        /// <summary>
        /// The map / grid coordinates of the mover entity.
        /// </summary>
        public readonly EntityCoordinates MoverCoordinates;

        public readonly TransformComponent Component;

        /// <summary>
        /// The cached <see cref="MapId"/> of the <see cref="MoverCoordinates"/>.
        /// </summary>
        public readonly MapId MapId;

        /// <summary>
        /// The cached <see cref="GridId"/> of the <see cref="MoverCoordinates"/>.
        /// </summary>
        public readonly GridId GridId;

        // This one is essentially for EntityLookup's recursion

        /// <summary>
        /// Local AABB of the mover entity.
        /// </summary>
        public readonly Box2 MoverAABB;

        public EntityMoveEvent(
            EntityUid entity,
            EntityCoordinates moverCoordinates,
            TransformComponent component,
            MapId mapId,
            GridId gridId,
            Box2 moverAABB)
        {
            Entity = entity;
            MoverCoordinates = moverCoordinates;
            Component = component;
            MapId = mapId;
            GridId = gridId;
            MoverAABB = moverAABB;
        }
    }
}

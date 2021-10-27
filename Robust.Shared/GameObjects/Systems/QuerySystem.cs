using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Dynamics;

namespace Robust.Shared.GameObjects
{
    /// <summary>
    /// A positional lookup system for entities.
    /// </summary>
    public sealed class QuerySystem : EntitySystem
    {
        /*
         * TODO:
         * Need support for containers as a flag
         * Need support for accurate bounds intersecting
         */

        [Dependency] private readonly IEntityLookup _lookup = default!;
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly SharedBroadphaseSystem _broadphaseSystem = default!;

        public const QueryFlags DefaultFlags = QueryFlags.Anchored | QueryFlags.EntityLookup;

        /*
         * The reason there's duplicate code between the EntityCoordinates / MapCoordinates / QueryPoint methods
         * is because each of the internally called methods is optimised for that use-case.
         */

        // Due to how GetEntitiesIntersecting works these AnyIntersecting methods will be slower than they need to be
        // but future problem.
        #region AnyIntersecting

        /// <summary>
        /// True if we find any entities overlapping.
        /// Does not necessarily mean these are overlapping for physics purposes.
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public bool AnyEntitiesIntersecting(EntityUid uid, QueryFlags flags = DefaultFlags)
        {
            return GetEntitiesIntersecting(uid, flags).Any();
        }

        public bool AnyEntitiesIntersecting(MapId mapId, Box2 worldAABB, QueryFlags flags = DefaultFlags)
        {
            return GetEntitiesIntersecting(mapId, worldAABB, flags).Any();
        }

        public bool AnyEntitiesIntersecting(MapId mapId, Box2Rotated worldBounds, QueryFlags flags = DefaultFlags)
        {
            return GetEntitiesIntersecting(mapId, worldBounds, flags).Any();
        }

        public bool AnyEntitiesIntersecting(EntityCoordinates coordinates, QueryFlags flags = DefaultFlags)
        {
            return GetEntitiesIntersecting(coordinates, flags).Any();
        }

        public bool AnyEntitiesIntersecting(MapCoordinates coordinates, QueryFlags flags = DefaultFlags)
        {
            return GetEntitiesIntersecting(coordinates, flags).Any();
        }

        public bool AnyEntitiesIntersecting(TileRef tileRef, QueryFlags flags = DefaultFlags)
        {
            return GetEntitiesIntersecting(tileRef, flags).Any();
        }

        #endregion

        #region GetIntersecting

        public IEnumerable<IEntity> GetEntitiesIntersecting(EntityUid entityUid, QueryFlags flags = DefaultFlags)
        {
            var xform = EntityManager.GetComponent<TransformComponent>(entityUid);
            var mapId = xform.MapID;

            if (mapId == MapId.Nullspace) yield break;

            var worldBounds = GetBounds(entityUid);
            var worldAABB = worldBounds.CalcBoundingBox();
            var ents = new HashSet<EntityUid>();

            if ((flags & QueryFlags.Anchored) != 0x0)
            {
                foreach (var grid in _mapManager.FindGridsIntersecting(mapId, worldBounds))
                {
                    foreach (var uid in grid.GetAnchoredEntities(worldBounds))
                    {
                        if (!EntityManager.EntityExists(uid)) continue;
                        ents.Add(uid);
                    }
                }
            }

            if ((flags & QueryFlags.EntityLookup) != 0x0)
            {
                var enumerator = _lookup.GetLookupsIntersecting(mapId, worldAABB);

                while (enumerator.MoveNext(out var lookup))
                {
                    var localAABB = GetLocalAABB(entityUid, lookup);

                    lookup.Tree._b2Tree.FastQuery(ref localAABB, (ref IEntity data) =>
                    {
                        if (data.Deleted) return;
                        ents.Add(data.Uid);
                    });
                }
            }

            if ((flags & QueryFlags.Physics) != 0x0)
            {
                foreach (var broadphase in _broadphaseSystem.GetBroadphases(mapId, worldAABB))
                {
                    var localAABB = broadphase.Owner.Transform.InvWorldMatrix.TransformBox(worldBounds);

                    foreach (var proxy in broadphase.Tree.QueryAabb(localAABB))
                    {
                        var uid = proxy.Fixture.Body.Owner.Uid;
                        if (!EntityManager.EntityExists(uid)) continue;

                        ents.Add(uid);
                    }
                }
            }

            foreach (var uid in ents)
            {
                yield return EntityManager.GetEntity(uid);
            }
        }

        public IEnumerable<IEntity> GetEntitiesIntersecting(MapId mapId, Box2 worldAABB, QueryFlags flags = DefaultFlags)
        {
            if (mapId == MapId.Nullspace) yield break;

            var ents = new HashSet<EntityUid>();

            if ((flags & QueryFlags.Anchored) != 0x0)
            {
                foreach (var grid in _mapManager.FindGridsIntersecting(mapId, worldAABB))
                {
                    foreach (var uid in grid.GetAnchoredEntities(worldAABB))
                    {
                        if (!EntityManager.EntityExists(uid)) continue;
                        ents.Add(uid);
                    }
                }
            }

            if ((flags & QueryFlags.EntityLookup) != 0x0)
            {
                var enumerator = _lookup.GetLookupsIntersecting(mapId, worldAABB);

                while (enumerator.MoveNext(out var lookup))
                {
                    var localAABB = lookup.Owner.Transform.InvWorldMatrix.TransformBox(worldAABB);

                    lookup.Tree._b2Tree.FastQuery(ref localAABB, (ref IEntity data) =>
                    {
                        if (data.Deleted) return;
                        ents.Add(data.Uid);
                    });
                }
            }

            if ((flags & QueryFlags.Physics) != 0x0)
            {
                foreach (var broadphase in _broadphaseSystem.GetBroadphases(mapId, worldAABB))
                {
                    var localAABB = broadphase.Owner.Transform.InvWorldMatrix.TransformBox(worldAABB);

                    foreach (var proxy in broadphase.Tree.QueryAabb(localAABB))
                    {
                        var uid = proxy.Fixture.Body.Owner.Uid;
                        if (!EntityManager.EntityExists(uid)) continue;

                        ents.Add(uid);
                    }
                }
            }

            foreach (var uid in ents)
            {
                yield return EntityManager.GetEntity(uid);
            }
        }

        public IEnumerable<IEntity> GetEntitiesIntersecting(MapId mapId, Box2Rotated worldBounds, QueryFlags flags = DefaultFlags)
        {
            if (mapId == MapId.Nullspace) yield break;

            var worldAABB = worldBounds.CalcBoundingBox();
            var ents = new HashSet<EntityUid>();

            if ((flags & QueryFlags.Anchored) != 0x0)
            {
                foreach (var grid in _mapManager.FindGridsIntersecting(mapId, worldBounds))
                {
                    foreach (var uid in grid.GetAnchoredEntities(worldBounds))
                    {
                        if (!EntityManager.EntityExists(uid)) continue;
                        ents.Add(uid);
                    }
                }
            }

            if ((flags & QueryFlags.EntityLookup) != 0x0)
            {
                var enumerator = _lookup.GetLookupsIntersecting(mapId, worldAABB);

                while (enumerator.MoveNext(out var lookup))
                {
                    var localAABB = lookup.Owner.Transform.InvWorldMatrix.TransformBox(worldBounds);

                    lookup.Tree._b2Tree.FastQuery(ref localAABB, (ref IEntity data) =>
                    {
                        if (data.Deleted) return;
                        ents.Add(data.Uid);
                    });
                }
            }

            if ((flags & QueryFlags.Physics) != 0x0)
            {
                foreach (var broadphase in _broadphaseSystem.GetBroadphases(mapId, worldAABB))
                {
                    var localAABB = broadphase.Owner.Transform.InvWorldMatrix.TransformBox(worldBounds);

                    foreach (var proxy in broadphase.Tree.QueryAabb(localAABB))
                    {
                        var uid = proxy.Fixture.Body.Owner.Uid;
                        if (!EntityManager.EntityExists(uid)) continue;

                        ents.Add(uid);
                    }
                }
            }

            foreach (var uid in ents)
            {
                yield return EntityManager.GetEntity(uid);
            }
        }

        public IEnumerable<IEntity> GetEntitiesIntersecting(EntityCoordinates coordinates, QueryFlags flags = DefaultFlags)
        {
            var mapCoordinates = coordinates.ToMap(EntityManager);
            return GetEntitiesIntersecting(mapCoordinates, flags);
        }

        public IEnumerable<IEntity> GetEntitiesIntersecting(MapCoordinates coordinates, QueryFlags flags = DefaultFlags)
        {
            if (coordinates.MapId == MapId.Nullspace) yield break;

            var mapId = coordinates.MapId;
            var ents = new HashSet<EntityUid>();

            if ((flags & QueryFlags.Anchored) != 0x0 &&
                _mapManager.TryFindGridAt(coordinates, out var grid))
            {
                foreach (var uid in grid.GetAnchoredEntities(coordinates))
                {
                    if (!EntityManager.EntityExists(uid)) continue;
                    ents.Add(uid);
                }
            }

            if ((flags & QueryFlags.EntityLookup) != 0x0)
            {
                var enumerator = _lookup.GetLookupsIntersecting(mapId, coordinates.Position);

                while (enumerator.MoveNext(out var lookup))
                {
                    var localPos = lookup.Owner.Transform.InvWorldMatrix.Transform(coordinates.Position);

                    foreach (var entity in lookup.Tree.QueryPoint(localPos))
                    {
                        if (entity.Deleted) continue;
                        ents.Add(entity.Uid);
                    }
                }
            }

            if ((flags & QueryFlags.Physics) != 0x0)
            {
                foreach (var broadphase in _broadphaseSystem.GetBroadphases(mapId, coordinates.Position))
                {
                    var localPos = broadphase.Owner.Transform.InvWorldMatrix.Transform(coordinates.Position);

                    broadphase.Tree.QueryPoint((in FixtureProxy data) =>
                    {
                        var uid = data.Fixture.Body.Owner.Uid;

                        if (!EntityManager.EntityExists(uid)) return true;

                        ents.Add(uid);

                        return true;
                    }, localPos);
                }
            }

            foreach (var uid in ents)
            {
                yield return EntityManager.GetEntity(uid);
            }
        }

        public IEnumerable<IEntity> GetEntitiesIntersecting(TileRef tileRef, QueryFlags flags = DefaultFlags)
        {
            return GetEntitiesIntersecting(tileRef.GridIndex, tileRef.GridIndices, flags);
        }

        public IEnumerable<IEntity> GetEntitiesIntersecting(GridId gridId, Vector2i gridIndices, QueryFlags flags = DefaultFlags)
        {
            // With tiles we can short-circuit and use the lookup directly without needing to enumerate.
            if (gridId == GridId.Invalid ||
                !_mapManager.TryGetGrid(gridId, out var grid)) yield break;

            var lookup = EntityManager.GetComponent<EntityLookupComponent>(grid.GridEntityId);
            var ents = new HashSet<EntityUid>();
            var localBounds = new Box2(gridIndices, gridIndices + 1);

            if ((flags & QueryFlags.EntityLookup) != 0x0)
            {
                lookup.Tree._b2Tree.FastQuery(ref localBounds, (ref IEntity data) =>
                {
                    if (data.Deleted) return;
                    ents.Add(data.Uid);
                });
            }

            if ((flags & QueryFlags.Anchored) != 0x0)
            {
                foreach (var ent in grid.GetAnchoredEntities(gridIndices))
                {
                    if (!EntityManager.EntityExists(ent)) continue;
                    ents.Add(ent);
                }
            }

            if ((flags & QueryFlags.Physics) != 0x0)
            {
                var broadphase = _broadphaseSystem.GetBroadphase(grid.GridEntityId);

                foreach (var proxy in broadphase.Tree.QueryAabb(localBounds))
                {
                    var uid = proxy.Fixture.Body.Owner.Uid;
                    if (!EntityManager.EntityExists(uid)) continue;

                    ents.Add(uid);
                }
            }

            foreach (var uid in ents)
            {
                yield return EntityManager.GetEntity(uid);
            }
        }

        #endregion

        #region GetInRange

        public IEnumerable<IEntity> GetEntitiesInRange(EntityUid entityUid, float range, QueryFlags flags = DefaultFlags)
        {
            var xform = EntityManager.GetComponent<TransformComponent>(entityUid);
            foreach (var entity in GetEntitiesInRange(xform.MapPosition, range, flags))
            {
                if (entity.Uid == entityUid) continue;
                yield return entity;
            }
        }

        public IEnumerable<IEntity> GetEntitiesInRange(EntityCoordinates coordinates, float range, QueryFlags flags = DefaultFlags)
        {
            var mapCoordinates = coordinates.ToMap(EntityManager);
            return GetEntitiesInRange(mapCoordinates, range, flags);
        }

        public IEnumerable<IEntity> GetEntitiesInRange(MapId mapId, Vector2 worldPos, float range,
            QueryFlags flags = DefaultFlags)
        {
            // TODO: Technically we should consider the edges of things but we need CollisionManager to be implemented
            // and even then it would rely heavily upon physics.

            if (mapId == MapId.Nullspace) yield break;

            var worldAABB = new Box2(worldPos - range, worldPos + range);
            var ents = new HashSet<EntityUid>();

            if ((flags & QueryFlags.Anchored) != 0x0)
            {
                foreach (var grid in _mapManager.FindGridsIntersecting(mapId, worldAABB))
                {
                    foreach (var uid in grid.GetAnchoredEntities(worldAABB))
                    {
                        if (!EntityManager.EntityExists(uid)) continue;
                        ents.Add(uid);
                    }
                }
            }

            if ((flags & QueryFlags.EntityLookup) != 0x0)
            {
                var enumerator = _lookup.GetLookupsIntersecting(mapId, worldAABB);

                while (enumerator.MoveNext(out var lookup))
                {
                    var localPos = lookup.Owner.Transform.InvWorldMatrix.Transform(worldPos);
                    var localAABB = new Box2(localPos - range, localPos + range);

                    lookup.Tree._b2Tree.FastQuery(ref localAABB, (ref IEntity data) =>
                    {
                        if (data.Deleted) return;
                        ents.Add(data.Uid);
                    });
                }
            }

            if ((flags & QueryFlags.Physics) != 0x0)
            {
                foreach (var broadphase in _broadphaseSystem.GetBroadphases(mapId, worldAABB))
                {
                    var localPos = broadphase.Owner.Transform.InvWorldMatrix.Transform(worldPos);
                    var localAABB = new Box2(localPos - range, localPos + range);

                    foreach (var proxy in broadphase.Tree.QueryAabb(localAABB))
                    {
                        var uid = proxy.Fixture.Body.Owner.Uid;

                        if (!EntityManager.EntityExists(uid)) continue;
                        ents.Add(uid);
                    }
                }
            }

            foreach (var uid in ents)
            {
                yield return EntityManager.GetEntity(uid);
            }
        }

        public IEnumerable<IEntity> GetEntitiesInRange(MapCoordinates coordinates, float range, QueryFlags flags = DefaultFlags)
        {
            return GetEntitiesInRange(coordinates.MapId, coordinates.Position, range, flags);
        }

        public IEnumerable<IEntity> GetEntitiesInRange(TileRef tileRef, float range, QueryFlags flags = DefaultFlags)
        {
            var coordinates = _mapManager.GetGrid(tileRef.GridIndex).InvWorldMatrix
                .Transform((Vector2) tileRef.GridIndices + 0.5f);

            return GetEntitiesInRange(new MapCoordinates(coordinates, tileRef.MapIndex), range, flags);
        }
        #endregion

        #region AABB methods

        /*
         * The reason we even consider Box2Rotated is because we may want "AABB" stuff to still function the same
         * regardless if a grid is rotated.
         */

        public Box2Rotated GetBounds(TileRef tileRef)
        {
            var grid = _mapManager.GetGrid(tileRef.GridIndex);
            var gridXform = EntityManager.GetComponent<TransformComponent>(grid.GridEntityId);

            var center = gridXform.WorldMatrix.Transform((Vector2) tileRef.GridIndices + 0.5f);

            return new Box2Rotated(Box2.UnitCentered.Translated(center), -gridXform.WorldRotation, center);
        }

        /// <summary>
        /// Gets the Axis-Aligned Bounding Box (AABB) for this entity.
        /// This AABB is not suitable for usage with rotated grids.
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public Box2 GetWorldAABB(EntityUid uid)
        {
            var xform = EntityManager.GetComponent<TransformComponent>(uid);
            var worldPos = xform.WorldPosition;

            if (EntityManager.TryGetComponent<PhysicsComponent>(uid, out var body))
            {
                var transform = new Transform(worldPos, xform.WorldRotation);
                Box2? aabb = null;

                foreach (var fixture in body.Fixtures)
                {
                    for (var i = 0; i < fixture.Shape.ChildCount; i++)
                    {
                        // if no aabb -> set it, otherwise union it.
                        aabb = aabb?.Union(fixture.Shape.ComputeAABB(transform, i)) ?? fixture.Shape.ComputeAABB(transform, i);
                    }
                }

                if (aabb != null)
                {
                    return aabb.Value;
                }
            }

            return new Box2(worldPos - float.Epsilon, worldPos + float.Epsilon);
        }

        private Box2 GetLocalAABB(EntityUid uid, EntityLookupComponent lookup)
        {
            var xform = EntityManager.GetComponent<TransformComponent>(uid);
            Vector2 localPos;

            if (xform.ParentUid == lookup.Owner.Uid)
            {
                localPos = xform.LocalPosition;
            }
            else
            {
                localPos = lookup.Owner.Transform.InvWorldMatrix.Transform(xform.WorldPosition);
            }

            var localRot = xform.WorldRotation - lookup.Owner.Transform.WorldRotation;

            if (EntityManager.TryGetComponent<PhysicsComponent>(uid, out var body))
            {
                var transform = new Transform(localPos, localRot);
                Box2? aabb = null;

                foreach (var fixture in body.Fixtures)
                {
                    for (var i = 0; i < fixture.Shape.ChildCount; i++)
                    {
                        // if no aabb -> set it, otherwise union it.
                        aabb = aabb?.Union(fixture.Shape.ComputeAABB(transform, i)) ?? fixture.Shape.ComputeAABB(transform, i);
                    }
                }

                if (aabb != null)
                {
                    return aabb.Value;
                }
            }

            return new Box2(localPos - float.Epsilon, localPos + float.Epsilon);
        }

        /// <summary>
        /// Get the world bounds of this entity.
        /// Rotation will be relative to its grid; if this is default will be relative to the map.
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        private Box2Rotated GetBounds(EntityUid uid)
        {
            var xform = EntityManager.GetComponent<TransformComponent>(uid);
            TransformComponent parentXform;

            if (xform.GridID == GridId.Invalid)
            {
                parentXform = EntityManager.GetComponent<TransformComponent>(_mapManager.GetMapEntity(xform.MapID).Uid);
            }
            else
            {
                parentXform = EntityManager.GetComponent<TransformComponent>(_mapManager.GetGrid(xform.GridID).GridEntityId);
            }

            if (EntityManager.TryGetComponent<PhysicsComponent>(uid, out var body))
            {
                var parentInvMatrix = parentXform.InvWorldMatrix;
                var localXform = parentInvMatrix.Transform(xform.WorldPosition);

                var aabb = new Box2();
                var localRot = xform.WorldRotation - parentXform.WorldRotation;
                var transform = new Transform(localXform, localRot);

                foreach (var fixture in body.Fixtures)
                {
                    for (var i = 0; i < fixture.Shape.ChildCount; i++)
                    {
                        aabb = aabb.IsEmpty() ?
                            fixture.Shape.ComputeAABB(transform, i) :
                            aabb.Union(fixture.Shape.ComputeAABB(transform, i));
                    }
                }

                var translatedAABB = parentXform.WorldMatrix.TransformBox(aabb);

                return new Box2Rotated(translatedAABB, -parentXform.WorldRotation, translatedAABB.Center);
            }

            var worldPos = xform.WorldPosition;

            return new Box2Rotated(Box2.UnitCentered.Translated(worldPos), -parentXform.WorldRotation, worldPos);
        }

        /// <summary>
        /// Gets the AABB of this entity relative to its grid.
        /// Will use the map if it is on the default grid.
        /// </summary>
        /// <param name="uid"></param>
        /// <returns></returns>
        public Box2 GetRelativeAABB(EntityUid uid)
        {
            var xform = EntityManager.GetComponent<TransformComponent>(uid);
            TransformComponent parentXform;

            if (xform.GridID == GridId.Invalid)
            {
                parentXform = EntityManager.GetComponent<TransformComponent>(_mapManager.GetMapEntity(xform.MapID).Uid);
            }
            else
            {
                parentXform = EntityManager.GetComponent<TransformComponent>(_mapManager.GetGrid(xform.GridID).GridEntityId);
            }

            var parentInvMatrix = parentXform.InvWorldMatrix;
            var localXform = parentInvMatrix.Transform(xform.WorldPosition);

            if (EntityManager.TryGetComponent<PhysicsComponent>(uid, out var body))
            {
                var aabb = new Box2();
                var localRot = xform.WorldRotation - parentXform.WorldRotation;
                var transform = new Transform(localXform, localRot);

                foreach (var fixture in body.Fixtures)
                {
                    for (var i = 0; i < fixture.Shape.ChildCount; i++)
                    {
                        aabb = aabb.IsEmpty() ?
                            fixture.Shape.ComputeAABB(transform, i) :
                            aabb.Union(fixture.Shape.ComputeAABB(transform, i));
                    }
                }

                return aabb;
            }

            return Box2.UnitCentered.Translated(localXform);
        }

        #endregion

        // TODO: Struct enumerators, also so we can short out AnyEntitiesIntersecting
    }

    /// <summary>
    /// Note: It's possible for duplicate entities to be returned if you query physics and other flags; uniqueness is not guaranteed.
    /// </summary>
    [Flags]
    public enum QueryFlags : ushort
    {
        None = 0,
        Anchored = 1 << 0,
        Physics = 1 << 1,
        EntityLookup = 1 << 2,
    }
}

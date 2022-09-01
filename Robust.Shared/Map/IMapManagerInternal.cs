using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Robust.Shared.Map
{
    /// <inheritdoc />
    internal interface IMapManagerInternal : IMapManager
    {
        IGameTiming GameTiming { get; }
        IEntityManager EntityManager { get; }

        void OnComponentRemoved(MapGridComponent comp);

        void ChunkRemoved(GridId gridId, MapChunk chunk);

        /// <summary>
        /// Specific version of TryFindGridAt that allows re-usable data structures to be passed in for optimisation reasons.
        /// </summary>
        bool TryFindGridAt(MapId mapId,
            Vector2 worldPos,
            List<MapGridComponent> grids,
            EntityQuery<TransformComponent> xformQuery,
            EntityQuery<PhysicsComponent> bodyQuery,
            [MaybeNullWhen(false)] out MapGridComponent grid);

        /// <summary>
        /// Specific version of FindGridsIntersecting that allows re-usable data structures to be passed in for optimisation reasons.
        /// </summary>
        IEnumerable<MapGridComponent> FindGridsIntersecting(MapId mapId,
            Box2 worldAabb,
            List<MapGridComponent> grids,
            EntityQuery<TransformComponent> xformQuery,
            EntityQuery<PhysicsComponent> physicsQuery,
            bool approx = false);

        /// <summary>
        ///     Raises the OnTileChanged event.
        /// </summary>
        /// <param name="tileRef">A reference to the new tile.</param>
        /// <param name="oldTile">The old tile that got replaced.</param>
        void RaiseOnTileChanged(TileRef tileRef, Tile oldTile);

        bool TryGetGridComp(GridId id, [NotNullWhen(true)] out MapGridComponent? comp);
        bool TryGetGridEuid(GridId id, [NotNullWhen(true)] out EntityUid? euid);
        void TrueGridDelete(MapGridComponent grid);
        void TrueDeleteMap(MapId mapId);
        GridId GenerateGridId(GridId? forcedGridId);
        void OnGridAllocated(MapGridComponent gridComponent, MapGridComponent mapGrid);
        void OnGridBoundsChange(EntityUid uid, MapGridComponent grid);
    }
}

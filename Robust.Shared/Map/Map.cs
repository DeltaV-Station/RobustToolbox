﻿using Robust.Shared.Maths;
using System.Collections.Generic;
using Robust.Shared.Timing;

namespace Robust.Shared.Map
{
    internal class Map : IMap
    {
        public GameTick CreatedTick { get; }
        public IMapGrid DefaultGrid { get; set; }
        public MapId Index { get; }

        private readonly IMapManagerInternal _mapManager;
        private readonly Dictionary<GridId, MapGrid> _grids = new Dictionary<GridId, MapGrid>();

        public Map(IMapManagerInternal mapManager, MapId mapID)
        {
            Index = mapID;
            _mapManager = mapManager;
            CreatedTick = _mapManager.GameTiming.CurTick;
        }

        /// <inheritdoc />
        public IMapGrid CreateGrid(GridId? gridId = null, ushort chunkSize = 16, float snapSize = 1)
        {
            return _mapManager.CreateGrid(Index, gridId);
        }

        public void AddGrid(MapGrid grid)
        {
            _grids.Add(grid.Index, grid);
        }

        public void RemoveGrid(MapGrid grid)
        {
            _grids.Remove(grid.Index);
        }

        /// <summary>
        ///     Checks if a grid exists with the given ID.
        /// </summary>
        /// <param name="gridId">The ID of the grid to check.</param>
        /// <returns></returns>
        public bool GridExists(GridId gridId)
        {
            return _grids.ContainsKey(gridId);
        }

        /// <summary>
        ///     Gets the grid associated with the given grid ID. If the grid with the given ID does not exist, return null.
        /// </summary>
        /// <param name="gridId">The id of the grid to get.</param>
        /// <returns></returns>
        public IMapGrid GetGrid(GridId gridId)
        {
            _grids.TryGetValue(gridId, out var output);
            return output;
        }

        public IEnumerable<IMapGrid> GetAllGrids()
        {
            return _grids.Values;
        }

        /// <inheritdoc />
        public IMapGrid FindGridAt(GridCoordinates worldPos)
        {
            var pos = worldPos.ToWorld(_mapManager).Position;
            return FindGridAt(pos);
        }

        /// <inheritdoc />
        public IMapGrid FindGridAt(Vector2 worldPos)
        {
            foreach (var kvGrid in _grids)
                if (kvGrid.Value.WorldBounds.Contains(worldPos) && kvGrid.Value != DefaultGrid)
                    return kvGrid.Value;
            return DefaultGrid;
        }

        /// <summary>
        ///     Finds all grids that intersect the rectangle in the world.
        /// </summary>
        /// <param name="worldArea">The are in world coordinates to search.</param>
        /// <returns></returns>
        public IEnumerable<IMapGrid> FindGridsIntersecting(Box2 worldArea)
        {
            var gridList = new List<MapGrid>();
            foreach (var kvGrid in _grids)
                if (kvGrid.Value.WorldBounds.Intersects(worldArea))
                    gridList.Add(kvGrid.Value);
            return gridList;
        }
    }
}

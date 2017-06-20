﻿using System.Collections.Generic;
using SFML.Graphics;
using SFML.System;
using SS14.Shared.IoC;
using SS14.Shared.Map;

namespace SS14.Shared.Interfaces.Map
{
    public delegate void TileChangedEventHandler(int gridId, TileRef tileRef, Tile oldTile);

    public interface IMapManager
    {
        /// <summary>
        /// A tile is being modified.
        /// </summary>
        event TileChangedEventHandler OnTileChanged;

        /// <summary>
        /// If you are only going to have one giant global grid, this is your gridId.
        /// </summary>
        int DefaultGridId { get; }

        // TODO: Do we need this property?
        /// <summary>
        /// Should the OnTileChanged event be suppressed? This is useful for initially loading the map 
        /// so that you don't spam an event for each of the million station tiles.
        /// </summary>
        bool SuppressOnTileChanged { get; set; }

        /// <summary>
        /// The length of the side of a square tile in world units.
        /// </summary>
        ushort TileSize { get; }
        
        //TODO: Map serializer/deserializer
        bool LoadMap(string mapName);
        void SaveMap(string mapName);

        #region GridAccess

        /// <summary>
        /// Creates a new empty grid with the given ID and optional chunk size. If a grid already 
        /// exists with the gridID, it is overwritten with the new grid.
        /// </summary>
        /// <param name="gridId">The id of the new grid to create.</param>
        /// <param name="chunkSize">Optional chunk size of the new grid.</param>
        /// <returns></returns>
        IMapGrid CreateGrid(int gridId, ushort chunkSize = 32);

        /// <summary>
        /// Checks if a grid exists with the given ID.
        /// </summary>
        /// <param name="gridId">The ID of the grid to check.</param>
        /// <returns></returns>
        bool GridExists(int gridId);

        /// <summary>
        /// Gets the grid associated with the given grid ID. If the grid with the given ID does not exist, return null.
        /// </summary>
        /// <param name="gridId">The id of the grid to get.</param>
        /// <returns></returns>
        IMapGrid GetGrid(int gridId);

        /// <summary>
        /// Gets the grid associated with the given grid ID.
        /// </summary>
        /// <param name="gridId">The id of the grid to get.</param>
        /// <param name="mapGrid">The grid associated with the grid ID. If no grid exists, this is null.</param>
        /// <returns></returns>
        bool TryGetGrid(int gridId, out IMapGrid mapGrid);

        /// <summary>
        /// Alias of IMapManager.GetGrid(IMapManager.DefaultGridId);
        /// </summary>
        /// <returns></returns>
        IMapGrid GetDefaultGrid();

        /// <summary>
        /// Deletes the grid associated with the given grid ID.
        /// </summary>
        /// <param name="gridId">The grid to remove.</param>
        void RemoveGrid(int gridId);

        /// <summary>
        /// Is there any grid at this position in the world?
        /// </summary>
        /// <param name="xWorld">The X coordinate in the world.</param>
        /// <param name="yWorld">The Y coordinate in the world.</param>
        /// <returns>True if there is any grid at the location.</returns>
        bool IsGridAt(float xWorld, float yWorld);

        /// <summary>
        /// Is the specified grid at this position in the world?
        /// </summary>
        /// <param name="xWorld">The X coordinate in the world.</param>
        /// <param name="yWorld">The Y coordinate in the world.</param>
        /// <param name="gridId">The grid id to find.</param>
        /// <returns></returns>
        bool IsGridAt(float xWorld, float yWorld, int gridId);

        /// <summary>
        /// Finds all of the grids at this position in the world.
        /// </summary>
        /// <param name="xWorld">The X coordinate in the world.</param>
        /// <param name="yWorld">The Y coordinate in the world.</param>
        /// <returns></returns>
        IEnumerable<IMapGrid> FindGridsAt(float xWorld, float yWorld);

        /// <summary>
        /// Finds all of the grids at this position in the world.
        /// </summary>
        /// <param name="posWorld">The location of the tile in world coordinates.</param>
        /// <returns></returns>
        IEnumerable<IMapGrid> FindGridsAt(Vector2f posWorld);

        /// <summary>
        /// Finds all grids that intersect the rectangle in the world.
        /// </summary>
        /// <param name="areaWorld">The are in world coordinates to search.</param>
        /// <returns></returns>
        IEnumerable<IMapGrid> FindGridsIntersecting(FloatRect areaWorld);

        #endregion
    }
}

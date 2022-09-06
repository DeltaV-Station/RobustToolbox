using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Robust.Shared.Map
{
    /// <summary>
    ///     This manages all of the grids in the world.
    /// </summary>
    public interface IMapManager
    {
        IEntityManager EntityManager { get; }

        IEnumerable<MapGridComponent> GetAllGrids()
        {
            return EntityManager.EntityQuery<MapGridComponent>();
        }

        /// <summary>
        /// Get the set of grids that have moved on this map in this tick.
        /// </summary>
        HashSet<MapGridComponent> GetMovedGrids(MapId mapId);

        /// <summary>
        /// Clear the set of grids that have moved on this map in this tick.
        /// </summary>
        void ClearMovedGrids(MapId mapId);

        /// <summary>
        ///     Starts up the map system.
        /// </summary>
        void Initialize();

        void Shutdown();
        void Startup();

        void Restart();

        /// <summary>
        ///     Creates a new map.
        /// </summary>
        /// <param name="mapId">
        ///     If provided, the new map will use this ID. If not provided, a new ID will be selected automatically.
        /// </param>
        /// <returns>The new map.</returns>
        /// <exception cref="InvalidOperationException">
        ///     Throw if an explicit ID for the map or default grid is passed and a map or grid with the specified ID already exists, respectively.
        /// </exception>
        MapId CreateMap(MapId? mapId = null);

        /// <summary>
        ///     Check whether a map with specified ID exists.
        /// </summary>
        /// <param name="mapId">The map ID to check existence of.</param>
        /// <returns>True if the map exists, false otherwise.</returns>
        bool MapExists(MapId mapId);

        /// <summary>
        /// Creates a new entity, then sets it as the map entity.
        /// </summary>
        /// <returns>Newly created entity.</returns>
        EntityUid CreateNewMapEntity(MapId mapId);

        /// <summary>
        /// Sets the MapEntity(root node) for a given map. If an entity is already set, it will be deleted
        /// before the new one is set.
        /// </summary>
        void SetMapEntity(MapId mapId, EntityUid newMapEntityId);

        /// <summary>
        /// Returns the map entity ID for a given map.
        /// </summary>
        EntityUid GetMapEntityId(MapId mapId);

        /// <summary>
        /// Replaces GetMapEntity()'s throw-on-failure semantics.
        /// </summary>
        EntityUid GetMapEntityIdOrThrow(MapId mapId);

        IEnumerable<MapId> GetAllMapIds();

        void DeleteMap(MapId mapId);

        MapGridComponent CreateGrid(MapId currentMapId, ushort chunkSize)
        {
            var gridEnt = EntityManager.SpawnEntity(null, new MapCoordinates(0, 0, currentMapId));
            var gridComp = EntityManager.AddComponent<MapGridComponent>(gridEnt);
            gridComp.ChunkSize = chunkSize;
            return gridComp;
        }

        MapGridComponent CreateGrid(MapId currentMapId)
        {
            var gridEnt = EntityManager.SpawnEntity(null, new MapCoordinates(0, 0, currentMapId));
            return EntityManager.AddComponent<MapGridComponent>(gridEnt);
        }

        MapGridComponent GetGrid(EntityUid gridId)
        {
            return GetGridComp(gridId);
        }

        bool TryGetGrid(EntityUid? euid, [MaybeNullWhen(false)] out MapGridComponent grid)
        {
            return EntityManager.TryGetComponent(euid, out grid);
        }

        bool GridExists([NotNullWhen(true)] EntityUid? euid)
        {
            return EntityManager.HasComponent<MapGridComponent>(euid);
        }

        IEnumerable<MapGridComponent> GetAllMapGrids(MapId mapId)
        {
            return EntityManager.EntityQuery<MapGridComponent, TransformComponent>(true)
                .Where(tuple => tuple.Item2.MapID == mapId)
                .Select(tuple => tuple.Item1);
        }

        /// <summary>
        /// Attempts to find the map grid under the map location.
        /// </summary>
        /// <remarks>
        /// This method will never return the map's default grid.
        /// </remarks>
        /// <param name="mapId">Map to search.</param>
        /// <param name="worldPos">Location on the map to check for a grid.</param>
        /// <param name="grid">Grid that was found, if any.</param>
        /// <returns>Returns true when a grid was found under the location.</returns>
        bool TryFindGridAt(MapId mapId, Vector2 worldPos, [MaybeNullWhen(false)] out MapGridComponent grid);

        /// <summary>
        /// Attempts to find the map grid under the map location.
        /// </summary>
        /// <remarks>
        /// This method will never return the map's default grid.
        /// </remarks>
        /// <param name="mapCoordinates">Location on the map to check for a grid.</param>
        /// <param name="grid">Grid that was found, if any.</param>
        /// <returns>Returns true when a grid was found under the location.</returns>
        bool TryFindGridAt(MapCoordinates mapCoordinates, [MaybeNullWhen(false)] out MapGridComponent grid);

        /// <summary>
        /// Returns the grids intersecting this AABB.
        /// </summary>
        /// <param name="mapId">The relevant MapID</param>
        /// <param name="worldAabb">The AABB to intersect</param>
        /// <param name="approx">Set to false if you wish to accurately get the grid bounds per-tile.</param>
        /// <returns></returns>
        IEnumerable<MapGridComponent> FindGridsIntersecting(MapId mapId, Box2 worldAabb, bool approx = false);

        /// <summary>
        /// Returns the grids intersecting this AABB.
        /// </summary>
        /// <param name="mapId">The relevant MapID</param>
        /// <param name="worldArea">The AABB to intersect</param>
        /// <param name="approx">Set to false if you wish to accurately get the grid bounds per-tile.</param>
        IEnumerable<MapGridComponent> FindGridsIntersecting(MapId mapId, Box2Rotated worldArea, bool approx = false);

        /// <summary>
        ///     A new map has been created.
        /// </summary>
        [Obsolete("Subscribe to MapChangedEvent on the event bus, and check if Created is true.")]
        event EventHandler<MapEventArgs> MapCreated;

        /// <summary>
        ///     An existing map has been destroyed.
        /// </summary>
        [Obsolete("Subscribe to MapChangedEvent on the event bus, and check if Destroyed is true.")]
        event EventHandler<MapEventArgs> MapDestroyed;

        bool HasMapEntity(MapId mapId);

        bool IsGrid(EntityUid uid)
        {
            return EntityManager.HasComponent<MapGridComponent>(uid);
        }

        bool IsMap(EntityUid uid);

        [Obsolete("Whatever this is used for, it is a terrible idea. Create a new map and get it's MapId.")]
        MapId NextMapId();

        MapGridComponent GetGridComp(EntityUid euid)
        {
            return EntityManager.GetComponent<MapGridComponent>(euid);
        }

        //
        // Pausing functions
        //

        void SetMapPaused(MapId mapId, bool paused);

        void DoMapInitialize(MapId mapId);

        void AddUninitializedMap(MapId mapId);

        [Pure]
        bool IsMapPaused(MapId mapId);

        [Pure]
        bool IsGridPaused(MapGridComponent grid);

        [Pure]
        bool IsGridPaused(EntityUid gridId);

        [Pure]
        bool IsMapInitialized(MapId mapId);
    }
}

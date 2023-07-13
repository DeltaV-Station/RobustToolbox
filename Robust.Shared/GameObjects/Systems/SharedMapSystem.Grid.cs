using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Map.Events;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Robust.Shared.GameObjects;

public abstract partial class SharedMapSystem
{
    #region Chunk helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2i GetChunkIndices(Vector2 tile, int chunkSize)
    {
        return new Vector2i ((int) Math.Floor(tile.X / chunkSize), (int) Math.Floor(tile.Y / chunkSize));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2i GetChunkIndices(Vector2 tile, byte chunkSize)
    {
        return new Vector2i ((int) Math.Floor(tile.X / chunkSize), (int) Math.Floor(tile.Y / chunkSize));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2i GetChunkIndices(Vector2i tile, int chunkSize)
    {
        return new Vector2i ((int) Math.Floor(tile.X / (float) chunkSize), (int) Math.Floor(tile.Y / (float) chunkSize));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2i GetChunkIndices(Vector2i tile, byte chunkSize)
    {
        return new Vector2i ((int) Math.Floor(tile.X / (float) chunkSize), (int) Math.Floor(tile.Y / (float) chunkSize));
    }

    /// <summary>
    /// Returns the tile offset to a chunk origin based on the provided size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2i GetChunkRelative(Vector2 tile, int chunkSize)
    {
        var x = MathHelper.Mod((int) Math.Floor(tile.X), chunkSize);
        var y = MathHelper.Mod((int) Math.Floor(tile.Y), chunkSize);
        return new Vector2i(x, y);
    }

    /// <summary>
    /// Returns the tile offset to a chunk origin based on the provided size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2i GetChunkRelative(Vector2 tile, byte chunkSize)
    {
        var x = MathHelper.Mod((int) Math.Floor(tile.X), chunkSize);
        var y = MathHelper.Mod((int) Math.Floor(tile.Y), chunkSize);
        return new Vector2i(x, y);
    }

    /// <summary>
    /// Returns the tile offset to a chunk origin based on the provided size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2i GetChunkRelative(Vector2i tile, int chunkSize)
    {
        var x = MathHelper.Mod(tile.X, chunkSize);
        var y = MathHelper.Mod(tile.Y, chunkSize);
        return new Vector2i(x, y);
    }

    /// <summary>
    /// Returns the tile offset to a chunk origin based on the provided size.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2i GetChunkRelative(Vector2i tile, byte chunkSize)
    {
        var x = MathHelper.Mod(tile.X, chunkSize);
        var y = MathHelper.Mod(tile.Y, chunkSize);
        return new Vector2i(x, y);
    }

    #endregion

    public static Vector2i GetDirection(Vector2i position, Direction dir, int dist = 1)
    {
        switch (dir)
        {
            case Direction.East:
                return position + new Vector2i(dist, 0);
            case Direction.SouthEast:
                return position + new Vector2i(dist, -dist);
            case Direction.South:
                return position + new Vector2i(0, -dist);
            case Direction.SouthWest:
                return position + new Vector2i(-dist, -dist);
            case Direction.West:
                return position + new Vector2i(-dist, 0);
            case Direction.NorthWest:
                return position + new Vector2i(-dist, dist);
            case Direction.North:
                return position + new Vector2i(0, dist);
            case Direction.NorthEast:
                return position + new Vector2i(dist, dist);
            default:
                throw new NotImplementedException();
        }
    }

    private void InitializeGrid()
    {
        SubscribeLocalEvent<MapGridComponent, ComponentGetState>(OnGridGetState);
        SubscribeLocalEvent<MapGridComponent, ComponentHandleState>(OnGridHandleState);
        SubscribeLocalEvent<MapGridComponent, ComponentAdd>(OnGridAdd);
        SubscribeLocalEvent<MapGridComponent, ComponentInit>(OnGridInit);
        SubscribeLocalEvent<MapGridComponent, ComponentStartup>(OnGridStartup);
        SubscribeLocalEvent<MapGridComponent, ComponentShutdown>(OnGridRemove);
        SubscribeLocalEvent<MapGridComponent, MoveEvent>(OnGridMove);
    }

    public void OnGridBoundsChange(EntityUid uid, MapGridComponent component)
    {
        // Just MapLoader things.
        if (component.MapProxy == DynamicTree.Proxy.Free) return;

        var xform = EntityManager.GetComponent<TransformComponent>(uid);
        var aabb = GetWorldAABB(uid, component, xform);

        if (TryComp<GridTreeComponent>(xform.MapUid, out var gridTree))
        {
            gridTree.Tree.MoveProxy(component.MapProxy, in aabb, Vector2.Zero);
        }

        if (TryComp<MovedGridsComponent>(xform.MapUid, out var movedGrids))
        {
            movedGrids.MovedGrids.Add(uid);
        }
    }

    private void OnGridMove(EntityUid uid, MapGridComponent component, ref MoveEvent args)
    {
        if (args.ParentChanged)
        {
            OnParentChange(uid, component, ref args);
            return;
        }

        // Just maploader / test things
        if (component.MapProxy == DynamicTree.Proxy.Free) return;

        var xform = args.Component;
        var aabb = GetWorldAABB(uid, component, xform);

        if (TryComp<GridTreeComponent>(xform.MapUid, out var gridTree))
        {
            gridTree.Tree.MoveProxy(component.MapProxy, in aabb, Vector2.Zero);
        }

        if (TryComp<MovedGridsComponent>(xform.MapUid, out var movedGrids))
        {
            movedGrids.MovedGrids.Add(uid);
        }
    }

    private void OnParentChange(EntityUid uid, MapGridComponent component, ref MoveEvent args)
    {
        if (EntityManager.HasComponent<MapComponent>(uid))
            return;

        var lifestage = EntityManager.GetComponent<MetaDataComponent>(uid).EntityLifeStage;

        // oh boy
        // Want gridinit to handle this hence specialcase those situations.
        // oh boy oh boy, its even worse now.
        // transform now raises parent change events on startup, because container code is a POS.
        if (lifestage < EntityLifeStage.Initialized || args.Component.LifeStage == ComponentLifeStage.Starting)
            return;

        // Make sure we cleanup old map for moved grid stuff.
        var mapId = args.Component.MapID;
        var oldMap = args.OldPosition.ToMap(EntityManager, _transform);

        // y'all need jesus
        if (oldMap.MapId == mapId) return;

        if (component.MapProxy != DynamicTree.Proxy.Free && TryComp<MovedGridsComponent>(MapManager.GetMapEntityId(oldMap.MapId), out var oldMovedGrids))
        {
            oldMovedGrids.MovedGrids.Remove(uid);
            RemoveGrid(uid, component, MapManager.GetMapEntityId(oldMap.MapId));
        }

        DebugTools.Assert(component.MapProxy == DynamicTree.Proxy.Free);
        if (TryComp<MovedGridsComponent>(MapManager.GetMapEntityId(mapId), out var newMovedGrids))
        {
            newMovedGrids.MovedGrids.Add(uid);
            AddGrid(uid, component, mapId);
        }
    }

    private void OnGridHandleState(EntityUid uid, MapGridComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not MapGridComponentState state)
            return;

        component.ChunkSize = state.ChunkSize;

        if (state.ChunkData == null && state.FullGridData == null)
            return;

        var modified = new List<(Vector2i position, Tile tile)>();
        MapManager.SuppressOnTileChanged = true;

        // delta state
        if (state.ChunkData != null)
        {
            foreach (var chunkData in state.ChunkData)
            {
                if (chunkData.IsDeleted())
                    continue;

                var chunk = GetOrAddChunk(uid, component, chunkData.Index);
                chunk.SuppressCollisionRegeneration = true;
                DebugTools.Assert(chunkData.TileData.Length == component.ChunkSize * component.ChunkSize);

                var counter = 0;
                for (ushort x = 0; x < component.ChunkSize; x++)
                {
                    for (ushort y = 0; y < component.ChunkSize; y++)
                    {
                        var tile = chunkData.TileData[counter++];
                        if (chunk.GetTile(x, y) == tile)
                            continue;

                        SetChunkTile(uid, component, chunk, x, y, tile);
                        modified.Add((new Vector2i(chunk.X * component.ChunkSize + x, chunk.Y * component.ChunkSize + y), tile));
                    }
                }
            }

            foreach (var chunkData in state.ChunkData)
            {
                if (chunkData.IsDeleted())
                {
                    RemoveChunk(uid, component, chunkData.Index);
                    continue;
                }

                var chunk = GetOrAddChunk(uid, component, chunkData.Index);
                chunk.SuppressCollisionRegeneration = false;
                RegenerateCollision(uid, component, chunk);
            }
        }

        // full state
        if (state.FullGridData != null)
        {
            foreach (var index in component.Chunks.Keys)
            {
                if (!state.FullGridData.ContainsKey(index))
                    RemoveChunk(uid, component, index);
            }

            foreach (var (index, tiles) in state.FullGridData)
            {
                var chunk = GetOrAddChunk(uid, component, index);
                chunk.SuppressCollisionRegeneration = true;
                DebugTools.Assert(tiles.Length == component.ChunkSize * component.ChunkSize);

                var counter = 0;
                for (ushort x = 0; x < component.ChunkSize; x++)
                {
                    for (ushort y = 0; y < component.ChunkSize; y++)
                    {
                        var tile = tiles[counter++];
                        if (chunk.GetTile(x, y) == tile)
                            continue;

                        SetChunkTile(uid, component, chunk, x, y, tile);
                        modified.Add((new Vector2i(chunk.X * component.ChunkSize + x, chunk.Y * component.ChunkSize + y), tile));
                    }
                }

                chunk.SuppressCollisionRegeneration = false;
                RegenerateCollision(uid, component, chunk);
            }
        }

        MapManager.SuppressOnTileChanged = false;
        if (modified.Count != 0)
            RaiseLocalEvent(uid, new GridModifiedEvent(component, modified), true);
    }

    private void OnGridGetState(EntityUid uid, MapGridComponent component, ref ComponentGetState args)
    {
        if (args.FromTick <= component.CreationTick)
        {
            GetFullState(uid, component, ref args);
            return;
        }

        List<ChunkDatum>? chunkData;
        var fromTick = args.FromTick;

        if (component.LastTileModifiedTick < fromTick)
        {
            chunkData = null;
        }
        else
        {
            chunkData = new List<ChunkDatum>();
            var chunks = component.ChunkDeletionHistory;

            foreach (var (tick, indices) in chunks)
            {
                if (tick < fromTick && fromTick != GameTick.Zero)
                    continue;

                chunkData.Add(ChunkDatum.CreateDeleted(indices));
            }

            foreach (var (index, chunk) in GetMapChunks(uid, component))
            {
                if (chunk.LastTileModifiedTick < fromTick)
                    continue;

                var tileBuffer = new Tile[component.ChunkSize * (uint) component.ChunkSize];

                // Flatten the tile array.
                // NetSerializer doesn't do multi-dimensional arrays.
                // This is probably really expensive.
                for (var x = 0; x < component.ChunkSize; x++)
                {
                    for (var y = 0; y < component.ChunkSize; y++)
                    {
                        tileBuffer[x * component.ChunkSize + y] = chunk.GetTile((ushort)x, (ushort)y);
                    }
                }
                chunkData.Add(ChunkDatum.CreateModified(index, tileBuffer));
            }
        }

        args.State = new MapGridComponentState(component.ChunkSize, chunkData);
    }

    private void GetFullState(EntityUid uid, MapGridComponent component, ref ComponentGetState args)
    {
        var chunkData = new Dictionary<Vector2i, Tile[]>();

        foreach (var (index, chunk) in GetMapChunks(uid, component))
        {
            var tileBuffer = new Tile[component.ChunkSize * (uint)component.ChunkSize];

            for (var x = 0; x < component.ChunkSize; x++)
            {
                for (var y = 0; y < component.ChunkSize; y++)
                {
                    tileBuffer[x * component.ChunkSize + y] = chunk.GetTile((ushort)x, (ushort)y);
                }
            }
            chunkData.Add(index, tileBuffer);
        }

        args.State = new MapGridComponentState(component.ChunkSize, chunkData);
    }

    private void OnGridAdd(EntityUid uid, MapGridComponent component, ComponentAdd args)
    {
        var msg = new GridAddEvent(uid);
        RaiseLocalEvent(uid, msg, true);
    }

    private void OnGridInit(EntityUid uid, MapGridComponent component, ComponentInit args)
    {
        var xformQuery = GetEntityQuery<TransformComponent>();
        var xform = xformQuery.GetComponent(uid);

        // Force networkedmapmanager to send it due to non-ECS legacy code.
        var curTick = _timing.CurTick;

        foreach (var chunk in component.Chunks.Values)
        {
            chunk.LastTileModifiedTick = curTick;
        }

        component.LastTileModifiedTick = curTick;

        if (xform.MapUid != null && xform.MapUid != uid)
            _transform.SetParent(uid, xform, xform.MapUid.Value, xformQuery);

        if (!HasComp<MapComponent>(uid))
        {
            var aabb = GetWorldAABB(uid, component);

            if (TryComp<GridTreeComponent>(xform.MapUid, out var gridTree))
            {
                var proxy = gridTree.Tree.CreateProxy(in aabb, (uid, component));
                DebugTools.Assert(component.MapProxy == DynamicTree.Proxy.Free);
                component.MapProxy = proxy;
            }

            if (TryComp<MovedGridsComponent>(xform.MapUid, out var movedGrids))
            {
                movedGrids.MovedGrids.Add(uid);
            }
        }

        var msg = new GridInitializeEvent(uid);
        RaiseLocalEvent(uid, msg, true);
    }

    private void OnGridStartup(EntityUid uid, MapGridComponent component, ComponentStartup args)
    {
        var msg = new GridStartupEvent(uid);
        RaiseLocalEvent(uid, msg, true);
    }

    private void OnGridRemove(EntityUid uid, MapGridComponent component, ComponentShutdown args)
    {
        if (TryComp<TransformComponent>(uid, out var xform) && xform.MapUid != null)
        {
            RemoveGrid(uid, component, xform.MapUid.Value);
        }

        component.MapProxy = DynamicTree.Proxy.Free;
        RaiseLocalEvent(uid, new GridRemovalEvent(uid), true);

        if (uid == EntityUid.Invalid)
            return;

        if (!MapManager.GridExists(uid))
            return;

        MapManager.DeleteGrid(uid);
    }

    private Box2 GetWorldAABB(EntityUid uid, MapGridComponent grid, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return new Box2();

        var (worldPos, worldRot) = _transform.GetWorldPositionRotation(xform);
        var aabb = grid.LocalAABB.Translated(worldPos);

        return new Box2Rotated(aabb, worldRot, worldPos).CalcBoundingBox();
    }

    private void AddGrid(EntityUid uid, MapGridComponent grid, MapId mapId)
    {
        DebugTools.Assert(!EntityManager.HasComponent<MapComponent>(uid));
        var aabb = GetWorldAABB(uid, grid);

        if (!_xformQuery.TryGetComponent(uid, out var xform))
            return;

        if (TryComp<GridTreeComponent>(xform.MapUid, out var gridTree))
        {
            var proxy = gridTree.Tree.CreateProxy(in aabb, (uid, grid));
            DebugTools.Assert(grid.MapProxy == DynamicTree.Proxy.Free);
            grid.MapProxy = proxy;
        }

        if (TryComp<MovedGridsComponent>(xform.MapUid, out var movedGrids))
        {
            movedGrids.MovedGrids.Add(uid);
        }
    }

    private void RemoveGrid(EntityUid uid, MapGridComponent grid, EntityUid mapUid)
    {
        if (grid.MapProxy != DynamicTree.Proxy.Free && TryComp<GridTreeComponent>(mapUid, out var gridTree))
        {
            gridTree.Tree.DestroyProxy(grid.MapProxy);
        }

        grid.MapProxy = DynamicTree.Proxy.Free;

        if (TryComp<MovedGridsComponent>(mapUid, out var movedGrids))
        {
            movedGrids.MovedGrids.Remove(uid);
        }
    }

    internal void RemoveChunk(EntityUid uid, MapGridComponent grid, Vector2i origin)
    {
        if (!grid.Chunks.TryGetValue(origin, out var chunk))
            return;

        if (_netManager.IsServer)
            grid.ChunkDeletionHistory.Add((_timing.CurTick, chunk.Indices));

        chunk.Fixtures.Clear();
        grid.Chunks.Remove(origin);

        if (grid.Chunks.Count == 0)
            RaiseLocalEvent(uid, new EmptyGridEvent { GridId = uid }, true);
    }

    /// <summary>
    /// Regenerates the chunk local bounds of this chunk.
    /// </summary>
    internal void RegenerateCollision(EntityUid uid, MapGridComponent grid, MapChunk mapChunk)
    {
        RegenerateCollision(uid, grid, new HashSet<MapChunk> { mapChunk });
    }

    /// <summary>
    /// Regenerate collision for multiple chunks at once; faster than doing it individually.
    /// </summary>
    internal void RegenerateCollision(EntityUid uid, MapGridComponent grid, IReadOnlySet<MapChunk> chunks)
    {
        if (HasComp<MapComponent>(uid))
            return;

        var chunkRectangles = new Dictionary<MapChunk, List<Box2i>>(chunks.Count);
        var removedChunks = new List<MapChunk>();

        foreach (var mapChunk in chunks)
        {
            // Even if the chunk is still removed still need to make sure bounds are updated (for now...)
            // generate collision rectangles for this chunk based on filled tiles.
            GridChunkPartition.PartitionChunk(mapChunk, out var localBounds, out var rectangles);
            mapChunk.CachedBounds = localBounds;

            if (mapChunk.FilledTiles > 0)
                chunkRectangles.Add(mapChunk, rectangles);
            else
            {
                // Gone. Reduced to atoms
                // Need to do this before RemoveChunk because it clears fixtures.
                FixturesComponent? manager = null;
                PhysicsComponent? body = null;
                TransformComponent? xform = null;

                foreach (var fixture in mapChunk.Fixtures)
                {
                    _fixtures.DestroyFixture(uid, fixture, false, manager: manager, body: body, xform: xform);
                }

                RemoveChunk(uid, grid, mapChunk.Indices);
                removedChunks.Add(mapChunk);
            }
        }

        grid.LocalAABB = new Box2();

        foreach (var chunk in grid.Chunks.Values)
        {
            var chunkBounds = chunk.CachedBounds;

            if (chunkBounds.Size.Equals(Vector2i.Zero))
                continue;

            if (grid.LocalAABB.Size == Vector2.Zero)
            {
                var gridBounds = chunkBounds.Translated(chunk.Indices * chunk.ChunkSize);
                grid.LocalAABB = gridBounds;
            }
            else
            {
                var gridBounds = chunkBounds.Translated(chunk.Indices * chunk.ChunkSize);
                grid.LocalAABB = grid.LocalAABB.Union(gridBounds);
            }
        }

        // May have been deleted from the bulk update above!
        if (Deleted(uid))
            return;

        _physics.WakeBody(uid);
        OnGridBoundsChange(uid, grid);
        _gridFixtures.RegenerateCollision(uid, chunkRectangles, removedChunks);
    }

    #region TileAccess

    public TileRef GetTileRef(EntityUid uid, MapGridComponent grid, MapCoordinates coords)
    {
        return GetTileRef(uid, grid, CoordinatesToTile(uid, grid, coords));
    }

    public TileRef GetTileRef(EntityUid uid, MapGridComponent grid, EntityCoordinates coords)
    {
        return GetTileRef(uid, grid, CoordinatesToTile(uid, grid, coords));
    }

    public TileRef GetTileRef(EntityUid uid, MapGridComponent grid, Vector2i tileCoordinates)
    {
        var chunkIndices = GridTileToChunkIndices(uid, grid, tileCoordinates);

        if (!grid.Chunks.TryGetValue(chunkIndices, out var output))
        {
            // Chunk doesn't exist, return a tileRef to an empty (space) tile.
            return new TileRef(uid, tileCoordinates.X, tileCoordinates.Y, default);
        }

        var chunkTileIndices = output.GridTileToChunkTile(tileCoordinates);
        return GetTileRef(uid, grid, output, (ushort)chunkTileIndices.X, (ushort)chunkTileIndices.Y);
    }

    /// <summary>
    ///     Returns the tile at the given chunk indices.
    /// </summary>
    /// <param name="mapChunk"></param>
    /// <param name="xIndex">The X tile index relative to the chunk origin.</param>
    /// <param name="yIndex">The Y tile index relative to the chunk origin.</param>
    /// <returns>A reference to a tile.</returns>
    internal TileRef GetTileRef(EntityUid uid, MapGridComponent grid, MapChunk mapChunk, ushort xIndex, ushort yIndex)
    {
        if (xIndex >= mapChunk.ChunkSize)
            throw new ArgumentOutOfRangeException(nameof(xIndex), "Tile indices out of bounds.");

        if (yIndex >= mapChunk.ChunkSize)
            throw new ArgumentOutOfRangeException(nameof(yIndex), "Tile indices out of bounds.");

        var indices = mapChunk.ChunkTileToGridTile(new Vector2i(xIndex, yIndex));
        return new TileRef(uid, indices, mapChunk.GetTile(xIndex, yIndex));
    }

    public IEnumerable<TileRef> GetAllTiles(EntityUid uid, MapGridComponent grid, bool ignoreEmpty = true)
    {
        foreach (var kvChunk in grid.Chunks)
        {
            var chunk = kvChunk.Value;
            for (ushort x = 0; x < grid.ChunkSize; x++)
            {
                for (ushort y = 0; y < grid.ChunkSize; y++)
                {
                    var tile = chunk.GetTile(x, y);

                    if (ignoreEmpty && tile.IsEmpty)
                        continue;

                    var (gridX, gridY) = new Vector2i(x, y) + chunk.Indices * grid.ChunkSize;
                    yield return new TileRef(uid, gridX, gridY, tile);
                }
            }
        }
    }

    public GridTileEnumerator GetAllTilesEnumerator(EntityUid uid, MapGridComponent grid, bool ignoreEmpty = true)
    {
        return new GridTileEnumerator(uid, grid.Chunks.GetEnumerator(), grid.ChunkSize, ignoreEmpty);
    }

    public void SetTile(EntityUid uid, MapGridComponent grid, EntityCoordinates coords, Tile tile)
    {
        var localTile = CoordinatesToTile(uid, grid, coords);
        SetTile(uid, grid, new Vector2i(localTile.X, localTile.Y), tile);
    }

    public void SetTile(EntityUid uid, MapGridComponent grid, Vector2i gridIndices, Tile tile)
    {
        var (chunk, chunkTile) = ChunkAndOffsetForTile(uid, grid, gridIndices);
        SetChunkTile(uid, grid, chunk, (ushort)chunkTile.X, (ushort)chunkTile.Y, tile);
        // Ideally we'd to this here for consistency but apparently tile modified does it or something.
        // Yeah it's noodly.
        // RegenerateCollision(chunk);
    }

    public void SetTiles(EntityUid uid, MapGridComponent grid, List<(Vector2i GridIndices, Tile Tile)> tiles)
    {
        if (tiles.Count == 0)
            return;

        var chunks = new HashSet<MapChunk>(Math.Max(1, tiles.Count / grid.ChunkSize));

        foreach (var (gridIndices, tile) in tiles)
        {
            var (chunk, chunkTile) = ChunkAndOffsetForTile(uid, grid, gridIndices);
            chunks.Add(chunk);
            chunk.SuppressCollisionRegeneration = true;
            SetChunkTile(uid, grid, chunk, (ushort)chunkTile.X, (ushort)chunkTile.Y, tile);
        }

        foreach (var chunk in chunks)
        {
            chunk.SuppressCollisionRegeneration = false;
        }

        RegenerateCollision(uid, grid, chunks);
    }

    public IEnumerable<TileRef> GetLocalTilesIntersecting(EntityUid uid, MapGridComponent grid, Box2Rotated localArea, bool ignoreEmpty = true,
        Predicate<TileRef>? predicate = null)
    {
        var localAABB = localArea.CalcBoundingBox();
        return GetLocalTilesIntersecting(uid, grid, localAABB, ignoreEmpty, predicate);
    }

    public IEnumerable<TileRef> GetTilesIntersecting(EntityUid uid, MapGridComponent grid, Box2Rotated worldArea, bool ignoreEmpty = true,
        Predicate<TileRef>? predicate = null)
    {
        var matrix = _transform.GetInvWorldMatrix(uid);
        var localArea = matrix.TransformBox(worldArea);

        foreach (var tile in GetLocalTilesIntersecting(uid, grid, localArea, ignoreEmpty, predicate))
        {
            yield return tile;
        }
    }

    public IEnumerable<TileRef> GetTilesIntersecting(EntityUid uid, MapGridComponent grid, Box2 worldArea, bool ignoreEmpty = true,
        Predicate<TileRef>? predicate = null)
    {
        var matrix = _transform.GetInvWorldMatrix(uid);
        var localArea = matrix.TransformBox(worldArea);

        foreach (var tile in GetLocalTilesIntersecting(uid, grid, localArea, ignoreEmpty, predicate))
        {
            yield return tile;
        }
    }

    public IEnumerable<TileRef> GetLocalTilesIntersecting(EntityUid uid, MapGridComponent grid, Box2 localArea, bool ignoreEmpty = true,
        Predicate<TileRef>? predicate = null)
    {
        // TODO: Should move the intersecting calls onto mapmanager system and then allow people to pass in xform / xformquery
        // that way we can avoid the GetComp here.
        var gridTileLb = new Vector2i((int)Math.Floor(localArea.Left), (int)Math.Floor(localArea.Bottom));
        // If we have 20.1 we want to include that tile but if we have 20 then we don't.
        var gridTileRt = new Vector2i((int)Math.Ceiling(localArea.Right), (int)Math.Ceiling(localArea.Top));

        for (var x = gridTileLb.X; x < gridTileRt.X; x++)
        {
            for (var y = gridTileLb.Y; y < gridTileRt.Y; y++)
            {
                var gridChunk = GridTileToChunkIndices(uid, grid, new Vector2i(x, y));

                if (grid.Chunks.TryGetValue(gridChunk, out var chunk))
                {
                    var chunkTile = chunk.GridTileToChunkTile(new Vector2i(x, y));
                    var tile = GetTileRef(uid, grid, chunk, (ushort)chunkTile.X, (ushort)chunkTile.Y);

                    if (ignoreEmpty && tile.Tile.IsEmpty)
                        continue;

                    if (predicate == null || predicate(tile))
                        yield return tile;
                }
                else if (!ignoreEmpty)
                {
                    var tile = new TileRef(uid, x, y, Tile.Empty);

                    if (predicate == null || predicate(tile))
                        yield return tile;
                }
            }
        }
    }

    public IEnumerable<TileRef> GetTilesIntersecting(EntityUid uid, MapGridComponent grid, Circle worldArea, bool ignoreEmpty = true,
        Predicate<TileRef>? predicate = null)
    {
        var aabb = new Box2(worldArea.Position.X - worldArea.Radius, worldArea.Position.Y - worldArea.Radius,
            worldArea.Position.X + worldArea.Radius, worldArea.Position.Y + worldArea.Radius);
        var circleGridPos = new EntityCoordinates(uid, WorldToLocal(uid, grid, worldArea.Position));

        foreach (var tile in GetTilesIntersecting(uid, grid, aabb, ignoreEmpty, predicate))
        {
            var local = GridTileToLocal(uid, grid, tile.GridIndices);

            if (!local.TryDistance(EntityManager, _transform, circleGridPos, out var distance))
            {
                continue;
            }

            if (distance <= worldArea.Radius)
            {
                yield return tile;
            }
        }
    }

    private bool TryGetTile(EntityUid uid, MapGridComponent grid, Vector2i indices, bool ignoreEmpty, [NotNullWhen(true)] out TileRef? tileRef, Predicate<TileRef>? predicate = null)
    {
        // Similar to TryGetTileRef but for the tiles intersecting iterators.
        var gridChunk = GridTileToChunkIndices(uid, grid, indices);

        if (grid.Chunks.TryGetValue(gridChunk, out var chunk))
        {
            var chunkTile = chunk.GridTileToChunkTile(indices);
            var tile = GetTileRef(uid, grid, chunk, (ushort)chunkTile.X, (ushort)chunkTile.Y);

            if (ignoreEmpty && tile.Tile.IsEmpty)
            {
                tileRef = null;
                return false;
            }

            if (predicate == null || predicate(tile))
            {
                tileRef = tile;
                return true;
            }
        }
        else if (!ignoreEmpty)
        {
            var tile = new TileRef(uid, indices.X, indices.Y, Tile.Empty);

            if (predicate == null || predicate(tile))
            {
                tileRef = tile;
                return true;
            }
        }

        tileRef = null;
        return false;
    }

    #endregion TileAccess

    #region ChunkAccess

    internal MapChunk GetOrAddChunk(EntityUid uid, MapGridComponent grid, int xIndex, int yIndex)
    {
        return GetOrAddChunk(uid, grid, new Vector2i(xIndex, yIndex));
    }

    internal bool TryGetChunk(EntityUid uid, MapGridComponent grid, Vector2i chunkIndices, [NotNullWhen(true)] out MapChunk? chunk)
    {
        return grid.Chunks.TryGetValue(chunkIndices, out chunk);
    }

    internal MapChunk GetOrAddChunk(EntityUid uid, MapGridComponent grid, Vector2i chunkIndices)
    {
        if (grid.Chunks.TryGetValue(chunkIndices, out var output))
            return output;

        var newChunk = new MapChunk(chunkIndices.X, chunkIndices.Y, grid.ChunkSize)
        {
            LastTileModifiedTick = _timing.CurTick
        };

        return grid.Chunks[chunkIndices] = newChunk;
    }

    public bool HasChunk(EntityUid uid, MapGridComponent grid, Vector2i chunkIndices)
    {
        return grid.Chunks.ContainsKey(chunkIndices);
    }

    internal IReadOnlyDictionary<Vector2i, MapChunk> GetMapChunks(EntityUid uid, MapGridComponent grid)
    {
        return grid.Chunks;
    }

    internal ChunkEnumerator GetMapChunks(EntityUid uid, MapGridComponent grid, Box2 worldAABB)
    {
        var localAABB = _transform.GetInvWorldMatrix(uid).TransformBox(worldAABB);
        return new ChunkEnumerator(grid.Chunks, localAABB, grid.ChunkSize);
    }

    internal ChunkEnumerator GetMapChunks(EntityUid uid, MapGridComponent grid, Box2Rotated worldArea)
    {
        var matrix = _transform.GetInvWorldMatrix(uid);
        var localArea = matrix.TransformBox(worldArea);
        return new ChunkEnumerator(grid.Chunks, localArea, grid.ChunkSize);
    }

    internal ChunkEnumerator GetLocalMapChunks(EntityUid uid, MapGridComponent grid, Box2 localAABB)
    {
        return new ChunkEnumerator(grid.Chunks, localAABB, grid.ChunkSize);
    }

    #endregion ChunkAccess

    #region SnapGridAccess

    public int AnchoredEntityCount(EntityUid uid, MapGridComponent grid, Vector2i pos)
    {
        var gridChunkPos = GridTileToChunkIndices(uid, grid, pos);

        if (!grid.Chunks.TryGetValue(gridChunkPos, out var chunk))
            return 0;

        var (x, y) = chunk.GridTileToChunkTile(pos);
        return chunk.GetSnapGrid((ushort)x, (ushort)y)?.Count ?? 0; // ?
    }

    public IEnumerable<EntityUid> GetAnchoredEntities(EntityUid uid, MapGridComponent grid, MapCoordinates coords)
    {
        return GetAnchoredEntities(uid, grid, TileIndicesFor(uid, grid, coords));
    }

    public IEnumerable<EntityUid> GetAnchoredEntities(EntityUid uid, MapGridComponent grid, EntityCoordinates coords)
    {
        return GetAnchoredEntities(uid, grid, TileIndicesFor(uid, grid, coords));
    }

    public IEnumerable<EntityUid> GetAnchoredEntities(EntityUid uid, MapGridComponent grid, Vector2i pos)
    {
        // Because some content stuff checks neighboring tiles (which may not actually exist) we won't just
        // create an entire chunk for it.
        var gridChunkPos = GridTileToChunkIndices(uid, grid, pos);

        if (!grid.Chunks.TryGetValue(gridChunkPos, out var chunk)) return Enumerable.Empty<EntityUid>();

        var chunkTile = chunk.GridTileToChunkTile(pos);
        return chunk.GetSnapGridCell((ushort)chunkTile.X, (ushort)chunkTile.Y);
    }

    public AnchoredEntitiesEnumerator GetAnchoredEntitiesEnumerator(EntityUid uid, MapGridComponent grid, Vector2i pos)
    {
        var gridChunkPos = GridTileToChunkIndices(uid, grid, pos);

        if (!grid.Chunks.TryGetValue(gridChunkPos, out var chunk)) return AnchoredEntitiesEnumerator.Empty;

        var chunkTile = chunk.GridTileToChunkTile(pos);
        var snapgrid = chunk.GetSnapGrid((ushort)chunkTile.X, (ushort)chunkTile.Y);

        return snapgrid == null
            ? AnchoredEntitiesEnumerator.Empty
            : new AnchoredEntitiesEnumerator(snapgrid.GetEnumerator());
    }

    public IEnumerable<EntityUid> GetLocalAnchoredEntities(EntityUid uid, MapGridComponent grid, Box2 localAABB)
    {
        foreach (var tile in GetLocalTilesIntersecting(uid, grid, localAABB, true, null))
        {
            foreach (var ent in GetAnchoredEntities(uid, grid, tile.GridIndices))
            {
                yield return ent;
            }
        }
    }

    public IEnumerable<EntityUid> GetAnchoredEntities(EntityUid uid, MapGridComponent grid, Box2 worldAABB)
    {
        foreach (var tile in GetTilesIntersecting(uid, grid, worldAABB))
        {
            foreach (var ent in GetAnchoredEntities(uid, grid, tile.GridIndices))
            {
                yield return ent;
            }
        }
    }

    public IEnumerable<EntityUid> GetAnchoredEntities(EntityUid uid, MapGridComponent grid, Box2Rotated worldBounds)
    {
        foreach (var tile in GetTilesIntersecting(uid, grid, worldBounds))
        {
            foreach (var ent in GetAnchoredEntities(uid, grid, tile.GridIndices))
            {
                yield return ent;
            }
        }
    }

    public Vector2i TileIndicesFor(EntityUid uid, MapGridComponent grid, EntityCoordinates coords)
    {
#if DEBUG
        var mapId = _xformQuery.GetComponent(uid).MapID;
        DebugTools.Assert(mapId == coords.GetMapId(EntityManager));
#endif

        return SnapGridLocalCellFor(uid, grid, LocalToGrid(uid, grid, coords));
    }

    public Vector2i TileIndicesFor(EntityUid uid, MapGridComponent grid, MapCoordinates worldPos)
    {
#if DEBUG
        var mapId = _xformQuery.GetComponent(uid).MapID;
        DebugTools.Assert(mapId == worldPos.MapId);
#endif

        var localPos = WorldToLocal(uid, grid, worldPos.Position);
        return SnapGridLocalCellFor(uid, grid, localPos);
    }

    private Vector2i SnapGridLocalCellFor(EntityUid uid, MapGridComponent grid, Vector2 localPos)
    {
        var x = (int)Math.Floor(localPos.X / grid.TileSize);
        var y = (int)Math.Floor(localPos.Y / grid.TileSize);
        return new Vector2i(x, y);
    }

    public bool IsAnchored(EntityUid uid, MapGridComponent grid, EntityCoordinates coords, EntityUid euid)
    {
        var tilePos = TileIndicesFor(uid, grid, coords);
        var (chunk, chunkTile) = ChunkAndOffsetForTile(uid, grid, tilePos);
        var snapgrid = chunk.GetSnapGrid((ushort)chunkTile.X, (ushort)chunkTile.Y);
        return snapgrid?.Contains(euid) == true;
    }

    public bool AddToSnapGridCell(EntityUid gridUid, MapGridComponent grid, Vector2i pos, EntityUid euid)
    {
        var (chunk, chunkTile) = ChunkAndOffsetForTile(gridUid, grid, pos);

        if (chunk.GetTile((ushort)chunkTile.X, (ushort)chunkTile.Y).IsEmpty)
            return false;

        chunk.AddToSnapGridCell((ushort)chunkTile.X, (ushort)chunkTile.Y, euid);
        return true;
    }

    public bool AddToSnapGridCell(EntityUid gridUid, MapGridComponent grid, EntityCoordinates coords, EntityUid euid)
    {
        return AddToSnapGridCell(gridUid, grid, TileIndicesFor(gridUid, grid, coords), euid);
    }

    public void RemoveFromSnapGridCell(EntityUid gridUid, MapGridComponent grid, Vector2i pos, EntityUid euid)
    {
        var (chunk, chunkTile) = ChunkAndOffsetForTile(gridUid, grid, pos);
        chunk.RemoveFromSnapGridCell((ushort)chunkTile.X, (ushort)chunkTile.Y, euid);
    }

    public void RemoveFromSnapGridCell(EntityUid gridUid, MapGridComponent grid, EntityCoordinates coords, EntityUid euid)
    {
        RemoveFromSnapGridCell(gridUid, grid, TileIndicesFor(gridUid, grid, coords), euid);
    }

    private (MapChunk, Vector2i) ChunkAndOffsetForTile(EntityUid uid, MapGridComponent grid, Vector2i pos)
    {
        var gridChunkIndices = GridTileToChunkIndices(uid, grid, pos);
        var chunk = GetOrAddChunk(uid, grid, gridChunkIndices);
        var chunkTile = chunk.GridTileToChunkTile(pos);
        return (chunk, chunkTile);
    }

    public IEnumerable<EntityUid> GetInDir(EntityUid uid, MapGridComponent grid, EntityCoordinates position, Direction dir)
    {
        var pos = GetDirection(TileIndicesFor(uid, grid, position), dir);
        return GetAnchoredEntities(uid, grid, pos);
    }

    public IEnumerable<EntityUid> GetOffset(EntityUid uid, MapGridComponent grid, EntityCoordinates coords, Vector2i offset)
    {
        var pos = TileIndicesFor(uid, grid, coords) + offset;
        return GetAnchoredEntities(uid, grid, pos);
    }

    public IEnumerable<EntityUid> GetLocal(EntityUid uid, MapGridComponent grid, EntityCoordinates coords)
    {
        return GetAnchoredEntities(uid, grid, TileIndicesFor(uid, grid, coords));
    }

    public EntityCoordinates DirectionToGrid(EntityUid uid, MapGridComponent grid, EntityCoordinates coords, Direction direction)
    {
        return GridTileToLocal(uid, grid, GetDirection(TileIndicesFor(uid, grid, coords), direction));
    }

    public IEnumerable<EntityUid> GetCardinalNeighborCells(EntityUid uid, MapGridComponent grid, EntityCoordinates coords)
    {
        var position = TileIndicesFor(uid, grid, coords);
        foreach (var cell in GetAnchoredEntities(uid, grid, position))
            yield return cell;
        foreach (var cell in GetAnchoredEntities(uid, grid, position + new Vector2i(0, 1)))
            yield return cell;
        foreach (var cell in GetAnchoredEntities(uid, grid, position + new Vector2i(0, -1)))
            yield return cell;
        foreach (var cell in GetAnchoredEntities(uid, grid, position + new Vector2i(1, 0)))
            yield return cell;
        foreach (var cell in GetAnchoredEntities(uid, grid, position + new Vector2i(-1, 0)))
            yield return cell;
    }

    public IEnumerable<EntityUid> GetCellsInSquareArea(EntityUid uid, MapGridComponent grid, EntityCoordinates coords, int n)
    {
        var position = TileIndicesFor(uid, grid, coords);

        for (var y = -n; y <= n; ++y)
        for (var x = -n; x <= n; ++x)
        {
            var enumerator = GetAnchoredEntitiesEnumerator(uid, grid, position + new Vector2i(x, y));

            while (enumerator.MoveNext(out var cell))
            {
                yield return cell.Value;
            }
        }
    }

    #endregion

    #region Transforms

    public Vector2 WorldToLocal(EntityUid uid, MapGridComponent grid, Vector2 posWorld)
    {
        var matrix = _transform.GetInvWorldMatrix(uid);
        return matrix.Transform(posWorld);
    }

    public EntityCoordinates MapToGrid(EntityUid uid, MapCoordinates posWorld)
    {
        var mapId = _xformQuery.GetComponent(uid).MapID;

        if (posWorld.MapId != mapId)
            throw new ArgumentException(
                $"Grid {uid} is on map {mapId}, but coords are on map {posWorld.MapId}.",
                nameof(posWorld));

        if (!TryComp<MapGridComponent>(uid, out var grid))
        {
            return new EntityCoordinates(MapManager.GetMapEntityId(posWorld.MapId), new Vector2(posWorld.X, posWorld.Y));
        }

        return new EntityCoordinates(uid, WorldToLocal(uid, grid, posWorld.Position));
    }

    public Vector2 LocalToWorld(EntityUid uid, MapGridComponent grid, Vector2 posLocal)
    {
        var matrix = _transform.GetWorldMatrix(uid);
        return matrix.Transform(posLocal);
    }

    public Vector2i WorldToTile(EntityUid uid, MapGridComponent grid, Vector2 posWorld)
    {
        var local = WorldToLocal(uid, grid, posWorld);
        var x = (int)Math.Floor(local.X / grid.TileSize);
        var y = (int)Math.Floor(local.Y / grid.TileSize);
        return new Vector2i(x, y);
    }

    public Vector2i LocalToTile(EntityUid uid, MapGridComponent grid, EntityCoordinates coordinates)
    {
        var position = LocalToGrid(uid, grid, coordinates);
        return new Vector2i((int) Math.Floor(position.X / grid.TileSize), (int) Math.Floor(position.Y / grid.TileSize));
    }

        public Vector2i CoordinatesToTile(EntityUid uid, MapGridComponent grid, MapCoordinates coords)
    {
#if DEBUG
        var mapId = _xformQuery.GetComponent(uid).MapID;
        DebugTools.Assert(mapId == coords.MapId);
#endif

        var local = WorldToLocal(uid, grid, coords.Position);

        var x = (int)Math.Floor(local.X / grid.TileSize);
        var y = (int)Math.Floor(local.Y / grid.TileSize);
        return new Vector2i(x, y);
    }

    public Vector2i CoordinatesToTile(EntityUid uid, MapGridComponent grid, EntityCoordinates coords)
    {
#if DEBUG
        var mapId = _xformQuery.GetComponent(uid).MapID;
        DebugTools.Assert(mapId == coords.GetMapId(EntityManager));
#endif
        var local = LocalToGrid(uid, grid, coords);

        var x = (int)Math.Floor(local.X / grid.TileSize);
        var y = (int)Math.Floor(local.Y / grid.TileSize);
        return new Vector2i(x, y);
    }

    public Vector2i LocalToChunkIndices(EntityUid uid, MapGridComponent grid, EntityCoordinates gridPos)
    {
        var local = LocalToGrid(uid, grid, gridPos);

        var x = (int)Math.Floor(local.X / (grid.TileSize * grid.ChunkSize));
        var y = (int)Math.Floor(local.Y / (grid.TileSize * grid.ChunkSize));
        return new Vector2i(x, y);
    }

    public Vector2 LocalToGrid(EntityUid uid, MapGridComponent grid, EntityCoordinates position)
    {
        return position.EntityId == uid
            ? position.Position
            : WorldToLocal(uid, grid, position.ToMapPos(EntityManager, _transform));
    }

    public bool CollidesWithGrid(EntityUid uid, MapGridComponent grid, Vector2i indices)
    {
        var chunkIndices = GridTileToChunkIndices(uid, grid, indices);
        if (!grid.Chunks.TryGetValue(chunkIndices, out var chunk))
            return false;

        var cTileIndices = chunk.GridTileToChunkTile(indices);
        return chunk.GetTile((ushort)cTileIndices.X, (ushort)cTileIndices.Y).TypeId != Tile.Empty.TypeId;
    }

    public Vector2i GridTileToChunkIndices(EntityUid uid, MapGridComponent grid, Vector2i gridTile)
    {
        var x = (int)Math.Floor(gridTile.X / (float) grid.ChunkSize);
        var y = (int)Math.Floor(gridTile.Y / (float) grid.ChunkSize);

        return new Vector2i(x, y);
    }

    public EntityCoordinates GridTileToLocal(EntityUid uid, MapGridComponent grid, Vector2i gridTile)
    {
        return new(uid,
            new Vector2(gridTile.X * grid.TileSize + (grid.TileSize / 2f), gridTile.Y * grid.TileSize + (grid.TileSize / 2f)));
    }

    public Vector2 GridTileToWorldPos(EntityUid uid, MapGridComponent grid, Vector2i gridTile)
    {
        var locX = gridTile.X * grid.TileSize + (grid.TileSize / 2f);
        var locY = gridTile.Y * grid.TileSize + (grid.TileSize / 2f);

        return _transform.GetWorldMatrix(uid).Transform(new Vector2(locX, locY));
    }

    public MapCoordinates GridTileToWorld(EntityUid uid, MapGridComponent grid, Vector2i gridTile)
    {
        var parentMapId = _xformQuery.GetComponent(uid).MapID;

        return new(GridTileToWorldPos(uid, grid, gridTile), parentMapId);
    }

    public bool TryGetTileRef(EntityUid uid, MapGridComponent grid, Vector2i indices, out TileRef tile)
    {
        var chunkIndices = GridTileToChunkIndices(uid, grid, indices);
        if (!grid.Chunks.TryGetValue(chunkIndices, out var chunk))
        {
            tile = default;
            return false;
        }

        var cTileIndices = chunk.GridTileToChunkTile(indices);
        tile = GetTileRef(uid, grid, chunk, (ushort)cTileIndices.X, (ushort)cTileIndices.Y);
        return true;
    }

    public bool TryGetTileRef(EntityUid uid, MapGridComponent grid, EntityCoordinates coords, out TileRef tile)
    {
        return TryGetTileRef(uid, grid, CoordinatesToTile(uid, grid, coords), out tile);
    }

    public bool TryGetTileRef(EntityUid uid, MapGridComponent grid, Vector2 worldPos, out TileRef tile)
    {
        return TryGetTileRef(uid, grid, WorldToTile(uid, grid, worldPos), out tile);
    }

    #endregion Transforms

    /// <summary>
    /// Calculate the world space AABB for this chunk.
    /// </summary>
    internal Box2 CalcWorldAABB(EntityUid uid, MapGridComponent grid, MapChunk mapChunk)
    {
        var (position, rotation) =
            _transform.GetWorldPositionRotation(uid);

        var chunkPosition = mapChunk.Indices;
        var tileScale = grid.TileSize;
        var chunkScale = mapChunk.ChunkSize;

        var worldPos = position + rotation.RotateVec(chunkPosition * tileScale * chunkScale);

        return new Box2Rotated(
            ((Box2)mapChunk.CachedBounds
                .Scale(tileScale))
            .Translated(worldPos),
            rotation, worldPos).CalcBoundingBox();
    }

    internal void OnTileModified(EntityUid uid, MapGridComponent grid, MapChunk mapChunk, Vector2i tileIndices, Tile newTile, Tile oldTile,
        bool shapeChanged)
    {
        // As the collision regeneration can potentially delete the chunk we'll notify of the tile changed first.
        var gridTile = mapChunk.ChunkTileToGridTile(tileIndices);
        mapChunk.LastTileModifiedTick = _timing.CurTick;
        grid.LastTileModifiedTick = _timing.CurTick;
        Dirty(grid);

        // The map serializer currently sets tiles of unbound grids as part of the deserialization process
        // It properly sets SuppressOnTileChanged so that the event isn't spammed for every tile on the grid.
        // ParentMapId is not able to be accessed on unbound grids, so we can't even call this function for unbound grids.
        if (!MapManager.SuppressOnTileChanged)
        {
            var newTileRef = new TileRef(uid, gridTile, newTile);
            _mapInternal.RaiseOnTileChanged(newTileRef, oldTile);
        }

        if (shapeChanged && !mapChunk.SuppressCollisionRegeneration)
        {
            RegenerateCollision(uid, grid, mapChunk);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Robust.Shared.Map;

internal interface INetworkedMapManager : IMapManagerInternal
{
    GameStateMapData? GetStateData(GameTick fromTick);
    void CullDeletionHistory(GameTick upToTick);

    // Two methods here, so that new grids etc can be made BEFORE entities get states applied,
    // but old ones can be deleted after.
    void ApplyGameStatePre(GameStateMapData? data, ReadOnlySpan<EntityState> entityStates);
}

internal sealed class NetworkedMapManager : MapManager, INetworkedMapManager
{
    private readonly Dictionary<GridId, List<(GameTick tick, Vector2i indices)>> _chunkDeletionHistory = new();

    public override void DeleteGrid(GridId gridId)
    {
        base.DeleteGrid(gridId);
        // No point syncing chunk removals anymore!
        _chunkDeletionHistory.Remove(gridId);
    }

    public override void ChunkRemoved(GridId gridId, MapChunk chunk)
    {
        base.ChunkRemoved(gridId, chunk);
        if (!_chunkDeletionHistory.TryGetValue(gridId, out var chunks))
        {
            chunks = new List<(GameTick tick, Vector2i indices)>();
            _chunkDeletionHistory[gridId] = chunks;
        }

        chunks.Add((GameTiming.CurTick, chunk.Indices));

        // Seemed easier than having this method on GridFixtureSystem
        if (!TryGetGrid(gridId, out var grid) ||
            !EntityManager.TryGetComponent(grid.GridEntityId, out PhysicsComponent? body) ||
            chunk.Fixtures.Count == 0)
            return;

        // TODO: Like MapManager injecting this is a PITA so need to work out an easy way to do it.
        // Maybe just add like a PostInject method that gets called way later?
        var fixtureSystem = EntitySystem.Get<FixtureSystem>();

        foreach (var fixture in chunk.Fixtures)
        {
            fixtureSystem.DestroyFixture(body, fixture);
        }
    }

    public GameStateMapData? GetStateData(GameTick fromTick)
    {
        var gridDatums = new Dictionary<GridId, GameStateMapData.GridDatum>();
        foreach (MapGrid grid in GetAllGrids())
        {
            if (grid.LastTileModifiedTick < fromTick)
                continue;

            var chunkData = new List<GameStateMapData.ChunkDatum>();

            if (_chunkDeletionHistory.TryGetValue(grid.Index, out var chunks))
            {
                foreach (var (tick, indices) in chunks)
                {
                    if (tick < fromTick)
                        continue;

                    chunkData.Add(GameStateMapData.ChunkDatum.CreateDeleted(indices));
                }
            }

            foreach (var (index, chunk) in grid.GetMapChunks())
            {
                if (chunk.LastTileModifiedTick < fromTick)
                    continue;

                var tileBuffer = new Tile[grid.ChunkSize * (uint)grid.ChunkSize];

                // Flatten the tile array.
                // NetSerializer doesn't do multi-dimensional arrays.
                // This is probably really expensive.
                for (var x = 0; x < grid.ChunkSize; x++)
                {
                    for (var y = 0; y < grid.ChunkSize; y++)
                    {
                        tileBuffer[x * grid.ChunkSize + y] = chunk.GetTile((ushort)x, (ushort)y);
                    }
                }
                chunkData.Add(GameStateMapData.ChunkDatum.CreateModified(index, tileBuffer));
            }

            var gridDatum = new GameStateMapData.GridDatum(
                    chunkData.ToArray(),
                    new MapCoordinates(grid.WorldPosition, grid.ParentMapId),
                    grid.WorldRotation);

            gridDatums.Add(grid.Index, gridDatum);
        }

        // no point sending empty collections
        if (gridDatums.Count == 0)
            return default;

        return new GameStateMapData(gridDatums.ToArray<KeyValuePair<GridId, GameStateMapData.GridDatum>>());
    }

    public void CullDeletionHistory(GameTick upToTick)
    {
        foreach (var (gridId, chunks) in _chunkDeletionHistory.ToArray())
        {
            chunks.RemoveAll(t => t.tick < upToTick);
            if (chunks.Count == 0)
                _chunkDeletionHistory.Remove(gridId);
        }
    }

    private readonly List<(MapId mapId, EntityUid euid)> _newMaps = new();
    private List<(MapId mapId, EntityUid euid, GridId gridId, ushort chunkSize)> _newGrids = new();

    public void ApplyGameStatePre(GameStateMapData? data, ReadOnlySpan<EntityState> entityStates)
    {
        // Setup new maps and grids
        {
            // search for any newly created map components
            foreach (var entityState in entityStates)
            {
                foreach (var compChange in entityState.ComponentChanges.Span)
                {
                    if (!compChange.Created)
                        continue;

                    if (compChange.State is MapComponentState mapCompState)
                    {
                        var mapEuid = entityState.Uid;
                        var mapId = mapCompState.MapId;

                        // map already exists from a previous state.
                        if (MapExists(mapId))
                            continue;

                        _newMaps.Add((mapId, mapEuid));
                    }
                    else if (data != null && data.GridData != null && compChange.State is MapGridComponentState gridCompState)
                    {
                        var gridEuid = entityState.Uid;
                        var gridId = gridCompState.GridIndex;
                        var chunkSize = gridCompState.ChunkSize;

                        // grid already exists from a previous state
                        if(GridExists(gridId))
                            continue;

                        DebugTools.Assert(chunkSize > 0, $"Invalid chunk size in entity state for new grid {gridId}.");

                        MapId gridMapId = default;
                        foreach (var kvData in data.GridData)
                        {
                            if (kvData.Key != gridId)
                                continue;

                            gridMapId = kvData.Value.Coordinates.MapId;
                            break;
                        }

                        DebugTools.Assert(gridMapId != default, $"Could not find corresponding gridData for new grid {gridId}.");

                        _newGrids.Add((gridMapId, gridEuid, gridId, chunkSize));
                    }
                }
            }

            // create all the new maps
            foreach (var (mapId, euid) in _newMaps)
            {
                CreateMap(mapId, euid);
            }
            _newMaps.Clear();

            // create all the new grids
            foreach (var (mapId, euid, gridId, chunkSize) in _newGrids)
            {
                CreateGrid(mapId, gridId, chunkSize, euid);
            }
            _newGrids.Clear();
        }

        // Process all grid updates.
        if (data != null && data.GridData != null)
        {
            // Ok good all the grids and maps exist now.
            foreach (var (gridId, gridDatum) in data.GridData)
            {
                var xformComp = EntityManager.GetComponent<TransformComponent>(gridId);
                ApplyTransformState(xformComp, gridDatum);

                var gridComp = EntityManager.GetComponent<IMapGridComponent>(gridId);
                MapGridComponent.ApplyMapGridState(this, gridComp, gridDatum.ChunkData);
            }
        }
    }

    private static void ApplyTransformState(TransformComponent xformComp, GameStateMapData.GridDatum gridDatum)
    {
        if (xformComp.MapID != gridDatum.Coordinates.MapId)
            throw new NotImplementedException("Moving grids between maps is not yet implemented");

        xformComp.WorldPosition = gridDatum.Coordinates.Position;
        xformComp.WorldRotation = gridDatum.Angle;
    }
}

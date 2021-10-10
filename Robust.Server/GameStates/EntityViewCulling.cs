using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.ObjectPool;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Players;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Robust.Server.GameStates
{
    internal sealed class EntityViewCulling
    {
        private const int ViewSetCapacity = 256; // starting number of entities that are in view
        private const int PlayerSetSize = 64; // Starting number of players
        private const int MaxVisPoolSize = 1024; // Maximum number of pooled objects

        private static readonly Vector2 Vector2NaN = new(float.NaN, float.NaN);

        private readonly IServerEntityManager _entMan;
        private readonly IMapManager _mapManager;
        private readonly IEntityLookup _lookup;

        private readonly Dictionary<ICommonSession, HashSet<EntityUid>> _playerVisibleSets = new(PlayerSetSize);
        internal readonly Dictionary<ICommonSession, Dictionary<IMapChunkInternal, GameTick>> PlayerChunks = new(PlayerSetSize);

        private readonly Dictionary<ICommonSession, ChunkStreamingData>
            _streamingChunks = new();

        internal int StreamingTilesPerTick;
        internal float StreamRange;

        private readonly ConcurrentDictionary<ICommonSession, GameTick> _playerLastFullMap = new();

        private readonly List<(GameTick tick, EntityUid uid)> _deletionHistory = new();

        private readonly ObjectPool<HashSet<EntityUid>> _visSetPool
            = new DefaultObjectPool<HashSet<EntityUid>>(new VisSetPolicy(), MaxVisPoolSize);

        private readonly ObjectPool<HashSet<EntityUid>> _viewerEntsPool
            = new DefaultObjectPool<HashSet<EntityUid>>(new DefaultPooledObjectPolicy<HashSet<EntityUid>>(), MaxVisPoolSize);

        private readonly ObjectPool<Dictionary<GridId, HashSet<IMapChunkInternal>>> _includedChunksPool =
            new DefaultObjectPool<Dictionary<GridId, HashSet<IMapChunkInternal>>>(new DefaultPooledObjectPolicy<Dictionary<GridId, HashSet<IMapChunkInternal>>>(), MaxVisPoolSize);

        private ushort _transformNetId = 0;

        /// <summary>
        /// Is view culling enabled, or will we send the whole map?
        /// </summary>
        public bool CullingEnabled { get; set; }

        /// <summary>
        /// Size of the side of the view bounds square.
        /// </summary>
        public float ViewSize { get; set; }

        private sealed class VisSetPolicy : PooledObjectPolicy<HashSet<EntityUid>>
        {
            public override HashSet<EntityUid> Create()
            {
                return new(ViewSetCapacity);
            }

            public override bool Return(HashSet<EntityUid> obj)
            {
                // TODO: This clear can be pretty expensive so maybe make a custom datatype given we're swapping
                // 70 - 300 entities a tick? Or do we even need to clear given it's just value types?
                obj.Clear();
                return true;
            }
        }

        public EntityViewCulling(IServerEntityManager entMan, IMapManager mapManager, IEntityLookup lookup)
        {
            _entMan = entMan;
            _mapManager = mapManager;
            _lookup = lookup;
        }

        public void SetTransformNetId(ushort value)
        {
            _transformNetId = value;
        }

        // Not thread safe
        public void EntityDeleted(EntityUid e)
        {
            // Not aware of prediction
            _deletionHistory.Add((_entMan.CurrentTick, e));
        }

        // Not thread safe
        public void CullDeletionHistory(GameTick oldestAck)
        {
            _deletionHistory.RemoveAll(hist => hist.tick < oldestAck);
        }

        private List<EntityUid> GetDeletedEntities(GameTick fromTick)
        {
            var list = new List<EntityUid>();
            foreach (var (tick, id) in _deletionHistory)
            {
                if (tick >= fromTick) list.Add(id);
            }

            return list;
        }

        // Not thread safe
        public void AddPlayer(ICommonSession session)
        {
            var visSet = _visSetPool.Get();

            _playerVisibleSets.Add(session, visSet);
            PlayerChunks.Add(session, new Dictionary<IMapChunkInternal, GameTick>(32));
            _streamingChunks.Add(session, new ChunkStreamingData());
        }

        // Not thread safe
        public void RemovePlayer(ICommonSession session)
        {
            _playerVisibleSets.Remove(session);
            PlayerChunks.Remove(session);
            _playerLastFullMap.Remove(session, out _);
            _streamingChunks.Remove(session);
        }

        // thread safe
        public bool IsPointVisible(ICommonSession session, in MapCoordinates position)
        {
            var viewables = GetSessionViewers(session);

            bool CheckInView(MapCoordinates mapCoordinates, HashSet<EntityUid> entityUids)
            {
                foreach (var euid in entityUids)
                {
                    var (viewBox, mapId) = CalcViewBounds(in euid);

                    if (mapId != mapCoordinates.MapId)
                        continue;

                    if (!CullingEnabled)
                        return true;

                    if (viewBox.Contains(mapCoordinates.Position))
                        return true;
                }

                return false;
            }

            bool result = CheckInView(position, viewables);

            viewables.Clear();
            _viewerEntsPool.Return(viewables);
            return result;
        }

        private HashSet<EntityUid> GetSessionViewers(ICommonSession session)
        {
            var viewers = _viewerEntsPool.Get();
            if (session.Status != SessionStatus.InGame || session.AttachedEntityUid is null)
                return viewers;

            viewers.Add(session.AttachedEntityUid.Value);

            // This is awful, but we're not gonna add the list of view subscriptions to common session.
            if (session is not IPlayerSession playerSession)
                return viewers;

            foreach (var uid in playerSession.ViewSubscriptions)
            {
                viewers.Add(uid);
            }

            return viewers;
        }

        // thread safe
        public (List<EntityState>? updates, List<EntityUid>? deletions) CalculateEntityStates(ICommonSession session, GameTick fromTick, GameTick toTick)
        {
            DebugTools.Assert(session.Status == SessionStatus.InGame);

            //TODO: Stop sending all entities to every player first tick
            List<EntityUid>? deletions;
            if (!CullingEnabled)
            {
                var allStates = ServerGameStateManager.GetAllEntityStates(_entMan, session, fromTick);
                deletions = GetDeletedEntities(fromTick);
                _playerLastFullMap.AddOrUpdate(session, toTick, (_, _) => toTick);
                return (allStates, deletions);
            }

            var visibleEnts = _visSetPool.Get();
            // As we may have entities parented to anchored entities on chunks we've never seen we need to make sure
            // they're included.
            List<EntityState> entityStates = new();

            //TODO: Refactor map system to not require every map and grid entity to function.
            IncludeMapCriticalEntities(visibleEnts);

            // if you don't have an attached entity, you don't see the world.
            if (session.AttachedEntityUid is not null)
            {
                var viewers = GetSessionViewers(session);
                var chunksSeen = PlayerChunks[session];

                foreach (var eyeEuid in viewers)
                {
                    var includedChunks = _includedChunksPool.Get();

                    var (viewBox, mapId) = CalcViewBounds(in eyeEuid);

                    uint visMask = 0;
                    if (_entMan.TryGetComponent<EyeComponent>(eyeEuid, out var eyeComp))
                        visMask = eyeComp.VisibilityMask;

                    //Always include the map entity of the eye, if it exists.
                    if(_mapManager.MapExists(mapId))
                        visibleEnts.Add(_mapManager.GetMapEntityId(mapId));

                    //Always include viewable ent itself
                    visibleEnts.Add(eyeEuid);

                    // grid entity should be added through this
                    // assume there are no deleted ents in here, cull them first in ent/comp manager
                    _lookup.FastEntitiesIntersecting(in mapId, ref viewBox, entity =>
                    {
                        RecursiveAdd(entity.Uid, visibleEnts, includedChunks, visMask);
                    }, LookupFlags.None);

                    //Calculate states for all visible anchored ents
                    GetDirtyChunksInRange(mapId, viewBox, chunksSeen, includedChunks);

                    var newChunkCount = GetAnchoredEntityStates(includedChunks, visibleEnts, visMask, chunksSeen, session, entityStates, fromTick);

                    // To fix pop-in we'll go through nearby chunks and send them little-by-little
                    StreamChunks(newChunkCount, session, chunksSeen, entityStates, viewBox, fromTick, mapId);

                    includedChunks.Clear();
                    _includedChunksPool.Return(includedChunks);
                }

                viewers.Clear();
                _viewerEntsPool.Return(viewers);
            }

            deletions = GetDeletedEntities(fromTick);
            GenerateEntityStates(entityStates, session, fromTick, visibleEnts, deletions);

            // no point sending an empty collection
            return (entityStates.Count == 0 ? default : entityStates, deletions.Count == 0 ? default : deletions);
        }

        // Look the reason I split these out is because it makes local profiling easier don't @ me
        private void GetDirtyChunksInRange(
            MapId mapId,
            Box2 viewBox,
            Dictionary<IMapChunkInternal, GameTick> chunksSeen,
            Dictionary<GridId, HashSet<IMapChunkInternal>> includedChunks)
        {
            foreach (var publicMapGrid in _mapManager.FindGridsIntersecting(mapId, viewBox))
            {
                var grid = (IMapGridInternal)publicMapGrid;

                // Can't really check when grid was modified here because we may need to dump new chunks on the person
                // as right now if you make a new chunk the client never actually gets these entities here.

                foreach (var chunk in grid.GetMapChunks(viewBox))
                {
                    // for each chunk, check dirty
                    if (chunksSeen.TryGetValue(chunk, out var chunkSeen) && chunk.LastAnchoredModifiedTick < chunkSeen)
                        continue;

                    if (!includedChunks.TryGetValue(grid.Index, out var chunks))
                    {
                        chunks = new HashSet<IMapChunkInternal>();
                        includedChunks[grid.Index] = chunks;
                    }

                    chunks.Add(chunk);
                }
            }
        }

        private int GetAnchoredEntityStates(
            Dictionary<GridId, HashSet<IMapChunkInternal>> includedChunks,
            HashSet<EntityUid> visibleEnts,
            uint visMask,
            Dictionary<IMapChunkInternal, GameTick> chunksSeen,
            ICommonSession session,
            List<EntityState> entityStates,
            GameTick fromTick)
        {
            var count = 0;

            foreach (var (gridId, chunks) in includedChunks)
            {
                // at least 1 anchored entity is going to be added, so add the grid (all anchored ents are parented to grid)
                RecursiveAdd(_mapManager.GetGrid(gridId).GridEntityId, visibleEnts, includedChunks, visMask);

                foreach (var chunk in chunks)
                {
                    if (!chunksSeen.TryGetValue(chunk, out var lastSeenChunk))
                    {
                        // Dump the whole thing on them.
                        lastSeenChunk = GameTick.Zero;
                        count++;
                    }

                    chunk.FastGetAllAnchoredEnts(uid =>
                    {
                        var ent = _entMan.GetEntity(uid);

                        if (ent.LastModifiedTick < lastSeenChunk)
                            return;

                        var newState = ServerGameStateManager.GetEntityState(_entMan, session, uid, lastSeenChunk);
                        entityStates.Add(newState);
                    });

                    chunksSeen[chunk] = fromTick;
                }
            }

            return count;
        }

        private void StreamChunks(
            int newChunkCount,
            ICommonSession session,
            Dictionary<IMapChunkInternal, GameTick> chunksSeen,
            List<EntityState> entityStates,
            Box2 viewBox,
            GameTick fromTick,
            MapId mapId)
        {
            if (newChunkCount == 0)
            {
                void StreamChunk(int iteration, IMapChunkInternal chunk, List<EntityState> entityStates)
                {
                    var chunkSize = chunk.ChunkSize;
                    var index = iteration * StreamingTilesPerTick;
                    // Logger.Debug($"Streaming chunk {chunk.Indices} iteration {iteration + 1}");

                    for (var i = index; i < Math.Min(index + StreamingTilesPerTick, chunkSize * chunkSize); i++)
                    {
                        var x = (ushort) (i / chunkSize);
                        var y = (ushort) (i % chunkSize);

                        foreach (var anchoredEnt in chunk.GetSnapGridCell(x, y))
                        {
                            var newState = ServerGameStateManager.GetEntityState(_entMan, session, anchoredEnt, GameTick.Zero);
                            entityStates.Add(newState);
                        }
                    }
                }

                var stream = _streamingChunks[session];

                // Check if we're already streaming a chunk and if so then continue it.
                if (stream.Chunk != null)
                {
                    // Came into PVS range so we'll stop streaming.
                    if (chunksSeen.ContainsKey(stream.Chunk))
                    {
                        stream.Chunk = null;
                        return;
                    }

                    var chunkSize = stream.Chunk.ChunkSize;
                    var streamsRequired = (int) MathF.Ceiling((chunkSize * chunkSize) / (float) StreamingTilesPerTick);

                    StreamChunk(stream.Iterations, stream.Chunk, entityStates);
                    stream.Iterations += 1;

                    // Chunk loaded in so we'll mark it as sent on the tick we started.
                    // Doesn't really matter if we send some duplicate data (e.g. entity changes since it started).
                    if (stream.Iterations >= streamsRequired)
                    {
                        chunksSeen[stream.Chunk] = stream.Tick;
                        stream.Chunk = null;
                    }

                    return;
                }

                if (StreamRange <= 0f) return;

                // Find a new chunk to start streaming in range.
                var enlarged = viewBox.Enlarged(StreamRange);

                foreach (var publicMapGrid in _mapManager.FindGridsIntersecting(mapId, enlarged))
                {
                    var grid = (IMapGridInternal) publicMapGrid;

                    foreach (var chunk in grid.GetMapChunks(enlarged))
                    {
                        // if we've ever seen this chunk don't worry about it.
                        if (chunksSeen.ContainsKey(chunk))
                            continue;

                        StreamChunk(0, chunk, entityStates);
                        // DebugTools.Assert(stream.Chunk == null);
                        stream.Chunk = chunk;
                        stream.Tick = fromTick;
                        stream.Iterations = 1;
                        break;
                    }

                    if (stream.Chunk != null) break;
                }
            }
        }

        private void GenerateEntityStates(List<EntityState> entityStates, ICommonSession session, GameTick fromTick, HashSet<EntityUid> currentSet, List<EntityUid> deletions)
        {
            // pretty big allocations :(
            var previousSet = _playerVisibleSets[session];

            // complement set
            foreach (var entityUid in previousSet)
            {
                // Still inside PVS
                if (currentSet.Contains(entityUid))
                    continue;

                // it was deleted, so we don't need to exit PVS
                if (deletions.Contains(entityUid))
                    continue;

                //TODO: HACK: somehow an entity left the view, transform does not exist (deleted?), but was not in the
                // deleted list. This seems to happen with the map entity on round restart.
                if (!_entMan.EntityExists(entityUid))
                    continue;

                var xform = _entMan.GetComponent<ITransformComponent>(entityUid);

                // Anchored entities don't ever leave
                if (xform.Anchored) continue;

                // PVS leave message
                //TODO: Remove NaN as the signal to leave PVS
                var oldState = (TransformComponent.TransformComponentState) xform.GetComponentState(session);

                entityStates.Add(new EntityState(entityUid,
                    new[]
                    {
                        ComponentChange.Changed(_transformNetId,
                            new TransformComponent.TransformComponentState(Vector2NaN,
                                oldState.Rotation,
                                oldState.ParentID,
                                oldState.NoLocalRotation,
                                oldState.Anchored)),
                    }));
            }

            foreach (var entityUid in currentSet)
            {

                // skip sending anchored entities (walls)
                DebugTools.Assert(!_entMan.GetComponent<ITransformComponent>(entityUid).Anchored);

                if (previousSet.Contains(entityUid))
                {
                    //Still Visible

                    // Nothing new to send
                    if(_entMan.GetEntity(entityUid).LastModifiedTick < fromTick)
                        continue;

                    // only send new changes
                    var newState = ServerGameStateManager.GetEntityState(_entMan, session, entityUid, fromTick);

                    if (!newState.Empty)
                        entityStates.Add(newState);
                }
                else
                {
                    // PVS enter message

                    // don't assume the client knows anything about us
                    var newState = ServerGameStateManager.GetEntityState(_entMan, session, entityUid, GameTick.Zero);
                    entityStates.Add(newState);
                }
            }

            // swap out vis sets
            _playerVisibleSets[session] = currentSet;
            _visSetPool.Return(previousSet);
        }

        private void IncludeMapCriticalEntities(ISet<EntityUid> set)
        {
            foreach (var mapId in _mapManager.GetAllMapIds())
            {
                if (_mapManager.HasMapEntity(mapId))
                {
                    set.Add(_mapManager.GetMapEntityId(mapId));
                }
            }

            foreach (var grid in _mapManager.GetAllGrids())
            {
                if (grid.GridEntityId != EntityUid.Invalid)
                {
                    set.Add(grid.GridEntityId);
                }
            }
        }

        // Read Safe

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private bool RecursiveAdd(EntityUid uid, HashSet<EntityUid> visSet, Dictionary<GridId, HashSet<IMapChunkInternal>> includedChunks, uint visMask)
        {
            // we are done, this ent has already been checked and is visible
            if (visSet.Contains(uid))
                return true;

            // if we are invisible, we are not going into the visSet, so don't worry about parents, and children are not going in
            if (_entMan.TryGetComponent<VisibilityComponent>(uid, out var visComp))
            {
                if ((visMask & visComp.Layer) == 0)
                    return false;
            }

            var xform = _entMan.GetComponent<TransformComponent>(uid);

            var parentUid = xform.ParentUid;

            // this is the world entity, it is always visible
            if (!parentUid.IsValid())
            {
                visSet.Add(uid);
                return true;
            }

            // parent is already in the set
            if (visSet.Contains(parentUid))
            {
                EnsureAnchoredChunk(xform, includedChunks);
                if (!xform.Anchored)
                    visSet.Add(uid);

                return true;
            }

            // parent was not added, so we are not either
            if (!RecursiveAdd(parentUid, visSet, includedChunks, visMask))
                return false;

            EnsureAnchoredChunk(xform, includedChunks);

            // add us
            if (!xform.Anchored)
                visSet.Add(uid);

            return true;
        }

        /// <summary>
        /// If we recursively get an anchored entity need to ensure the entire chunk is included (as it may be out of view).
        /// </summary>
        private void EnsureAnchoredChunk(TransformComponent xform, Dictionary<GridId, HashSet<IMapChunkInternal>> includedChunks)
        {
            // If we recursively get an anchored entity need to ensure the entire chunk is included (as it may be out of view).
            if (!xform.Anchored) return;

            // This is slow but entities being parented to anchored ones is hopefully rare so shouldn't be hit too much.
            var mapGrid = (IMapGridInternal) _mapManager.GetGrid(xform.GridID);
            var local = xform.Coordinates;
            var chunk = mapGrid.GetChunk(mapGrid.LocalToChunkIndices(local));

            if (!includedChunks.TryGetValue(xform.GridID, out var chunks))
            {
                chunks = new HashSet<IMapChunkInternal>();
                includedChunks[xform.GridID] = chunks;
            }

            chunks.Add(chunk);
        }

        // Read Safe
        private (Box2 view, MapId mapId) CalcViewBounds(in EntityUid euid)
        {
            var xform = _entMan.GetComponent<ITransformComponent>(euid);

            var view = Box2.UnitCentered.Scale(ViewSize).Translated(xform.WorldPosition);
            var map = xform.MapID;

            return (view, map);
        }

        // Dumped this in its own data structure just to avoid thread-safety issues.
        private sealed class ChunkStreamingData
        {
            public IMapChunkInternal? Chunk { get; set; }
            /// <summary>
            /// Tick when we started streaming the chunk.
            /// </summary>
            public GameTick Tick { get; set; }

            /// <summary>
            /// How many iterations we have done so far.
            /// </summary>
            public int Iterations { get; set; }

        }
    }
}

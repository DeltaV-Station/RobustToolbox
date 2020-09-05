using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Robust.Server.GameObjects.EntitySystemMessages;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.GameObjects.Components.Transform;
using Robust.Shared.GameObjects.EntitySystemMessages;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Robust.Server.GameObjects.EntitySystems.TileLookup
{
    /// <summary>
    ///     Stores what entities intersect a particular tile.
    /// </summary>
    [UsedImplicitly]
    public sealed class GridTileLookupSystem : EntitySystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        
        private readonly Dictionary<GridId, Dictionary<MapIndices, GridTileLookupChunk>> _graph = 
                     new Dictionary<GridId, Dictionary<MapIndices, GridTileLookupChunk>>();

        /// <summary>
        ///     Need to store the nodes for each entity because if the entity is deleted its transform is no longer valid.
        /// </summary>
        private readonly Dictionary<IEntity, HashSet<GridTileLookupNode>> _lastKnownNodes = 
                     new Dictionary<IEntity, HashSet<GridTileLookupNode>>();

        /// <summary>
        ///     Yields all of the entities intersecting a particular entity's tiles.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public IEnumerable<IEntity> GetEntitiesIntersecting(IEntity entity)
        {
            if (entity.Transform.GridID == GridId.Invalid)
            {
                throw new InvalidOperationException("Can't get grid tile intersecting entities for invalid grid");
            }
        
            foreach (var node in GetOrCreateNodes(entity))
            {
                foreach (var ent in node.Entities)
                {
                    yield return ent;
                }
            }
        }

        /// <summary>
        ///     Yields all of the entities intersecting a particular MapIndices
        /// </summary>
        /// <param name="gridId"></param>
        /// <param name="gridIndices"></param>
        /// <returns></returns>
        public IEnumerable<IEntity> GetEntitiesIntersecting(GridId gridId, MapIndices gridIndices)
        {
            if (gridId == GridId.Invalid)
            {
                throw new InvalidOperationException("Can't get grid tile intersecting entities for invalid grid");
            }
            
            if (!_graph.TryGetValue(gridId, out var chunks))
            {
                throw new InvalidOperationException("Unable to find grid for TileLookup");
            }

            var chunkIndices = GetChunkIndices(gridIndices);

            if (!chunks.TryGetValue(chunkIndices, out var chunk))
            {
                yield break;
            }

            foreach (var entity in chunk.GetNode(gridIndices).Entities)
            {
                yield return entity;
            }
        }

        private GridTileLookupChunk GetOrCreateChunk(GridId gridId, MapIndices indices)
        {
            var chunkIndices = GetChunkIndices(indices);

            if (!_graph.TryGetValue(gridId, out var gridChunks))
            {
                gridChunks = new Dictionary<MapIndices, GridTileLookupChunk>();
                _graph[gridId] = gridChunks;
            }

            if (!gridChunks.TryGetValue(chunkIndices, out var chunk))
            {
                chunk = new GridTileLookupChunk(gridId, chunkIndices);
                gridChunks[chunkIndices] = chunk;
            }

            return chunk;
        }

        private MapIndices GetChunkIndices(MapIndices indices)
        {
            return new MapIndices(
                (int) (Math.Floor((float) indices.X / GridTileLookupChunk.ChunkSize) * GridTileLookupChunk.ChunkSize),
                (int) (Math.Floor((float) indices.Y / GridTileLookupChunk.ChunkSize) * GridTileLookupChunk.ChunkSize));
        }

        private HashSet<GridTileLookupNode> GetOrCreateNodes(IEntity entity)
        {
            if (_lastKnownNodes.TryGetValue(entity, out var nodes))
            {
                return nodes;
            }

            if (entity.Deleted)
            {
                throw new InvalidOperationException($"Can't get nodes for deleted entity {entity.Name}!");
            }
            
            var grids = GetEntityIndices(entity);
            var results = new HashSet<GridTileLookupNode>();

            foreach (var (grid, indices) in grids)
            {
                foreach (var index in indices)
                {
                    results.Add(GetOrCreateNode(grid, index));
                }
            }

            _lastKnownNodes[entity] = results;
            return results;
        }

        private HashSet<GridTileLookupNode> GetOrCreateNodes(GridCoordinates gridCoordinates, Box2 box)
        {
            if (gridCoordinates.GridID == GridId.Invalid)
            {
                throw new InvalidOperationException("Cannot get TileLookup nodes for an InvalidGrid!");
            }
            
            var results = new HashSet<GridTileLookupNode>();
            
            foreach (var grid in _mapManager.FindGridsIntersecting(_mapManager.GetGrid(gridCoordinates.GridID).ParentMapId, box))
            {
                foreach (var tile in grid.GetTilesIntersecting(box))
                {
                    results.Add(GetOrCreateNode(grid.Index, tile.GridIndices));
                }
            }
            
            return results;
        }

        /// <summary>
        ///     Return the corresponding TileLookupNode for these indices
        /// </summary>
        /// <param name="gridId"></param>
        /// <param name="indices"></param>
        /// <returns></returns>
        private GridTileLookupNode GetOrCreateNode(GridId gridId, MapIndices indices)
        {
            var chunk = GetOrCreateChunk(gridId, indices);

            return chunk.GetNode(indices);
        }

        /// <summary>
        ///     Get the relevant GridId and MapIndices for this entity for lookup.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        private Dictionary<GridId, List<MapIndices>> GetEntityIndices(IEntity entity)
        {
            var entityBounds = GetEntityBox(entity);
            var results = new Dictionary<GridId, List<MapIndices>>();
            
            foreach (var grid in _mapManager.FindGridsIntersecting(entity.Transform.MapID, GetEntityBox(entity)))
            {
                var indices = new List<MapIndices>();

                foreach (var tile in grid.GetTilesIntersecting(entityBounds))
                {
                    indices.Add(tile.GridIndices);
                }
                
                results[grid.Index] = indices;
            }
            
            return results;
        }

        private Box2 GetEntityBox(IEntity entity)
        {
            // Need to clip the aabb as anything with an edge intersecting another tile might be picked up, such as walls.
            if (entity.TryGetComponent(out ICollidableComponent? collidableComponent))
                return new Box2(collidableComponent.WorldAABB.BottomLeft + 0.01f, collidableComponent.WorldAABB.TopRight - 0.01f);

            // Don't want to accidentally get neighboring tiles unless we're near an edge
            return Box2.CenteredAround(entity.Transform.GridPosition.Position, Vector2.One / 2);
        }

        public override void Initialize()
        {
            SubscribeLocalEvent<MoveEvent>(HandleEntityMove);
            SubscribeLocalEvent<EntityInitializedMessage>(HandleEntityInitialized);
            SubscribeLocalEvent<EntityDeletedMessage>(HandleEntityDeleted);
            _mapManager.OnGridRemoved += HandleGridRemoval;
        }

        private void HandleEntityInitialized(EntityInitializedMessage message)
        {
            HandleEntityAdd(message.Entity);
        }

        private void HandleEntityDeleted(EntityDeletedMessage message)
        {
            HandleEntityRemove(message.Entity);
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _mapManager.OnGridRemoved -= HandleGridRemoval;
        }

        private void HandleGridRemoval(GridId gridId)
        {
            var toRemove = new List<IEntity>();
            
            foreach (var (entity, _) in _lastKnownNodes)
            {
                if (entity.Transform.GridID == gridId)
                    toRemove.Add(entity);
            }

            foreach (var entity in toRemove)
            {
                _lastKnownNodes.Remove(entity);
            }
            
            _graph.Remove(gridId);
        }

        /// <summary>
        ///     Tries to add the entity to the relevant TileLookupNode
        /// </summary>
        /// The node will filter it to the correct category (if possible)
        /// <param name="entity"></param>
        private void HandleEntityAdd(IEntity entity)
        {
            if (entity.Deleted || entity.Transform.GridID == GridId.Invalid)
            {
                return;
            }

            var entityNodes = GetOrCreateNodes(entity);
            var newIndices = new Dictionary<GridId, List<MapIndices>>();
            
            foreach (var node in entityNodes)
            {
                node.AddEntity(entity);
                if (!newIndices.TryGetValue(node.ParentChunk.GridId, out var existing))
                {
                    existing = new List<MapIndices>();
                    newIndices[node.ParentChunk.GridId] = existing;
                }
                
                existing.Add(node.Indices);
            }

            _lastKnownNodes[entity] = entityNodes;
            EntityManager.EventBus.RaiseEvent(EventSource.Local, new TileLookupUpdateMessage(newIndices));
        }

        /// <summary>
        ///     Removes this entity from all of the applicable nodes.
        /// </summary>
        /// <param name="entity"></param>
        private void HandleEntityRemove(IEntity entity)
        {
            if (_lastKnownNodes.TryGetValue(entity, out var nodes))
            {
                foreach (var node in nodes)
                {
                    node.RemoveEntity(entity);
                }
            }

            _lastKnownNodes.Remove(entity);
            EntityManager.EventBus.RaiseEvent(EventSource.Local, new TileLookupUpdateMessage(null));
        }

        /// <summary>
        ///     When an entity moves around we'll remove it from its old node and add it to its new node (if applicable)
        /// </summary>
        /// <param name="moveEvent"></param>
        private void HandleEntityMove(MoveEvent moveEvent)
        {
            if (moveEvent.Sender.Deleted || moveEvent.NewPosition.GridID == GridId.Invalid)
            {
                HandleEntityRemove(moveEvent.Sender);
                return;
            }

            if (!_lastKnownNodes.TryGetValue(moveEvent.Sender, out var oldNodes))
            {
                return;
            }

            // Memory leak protection
            var gridBounds = _mapManager.GetGrid(moveEvent.Sender.Transform.GridID).WorldBounds;
            if (!gridBounds.Contains(moveEvent.Sender.Transform.WorldPosition))
            {
                HandleEntityRemove(moveEvent.Sender);
                return;
            }

            var bounds = GetEntityBox(moveEvent.Sender);
            var newNodes = GetOrCreateNodes(moveEvent.NewPosition, bounds);

            if (oldNodes.Count == newNodes.Count && oldNodes.SetEquals(newNodes))
            {
                return;
            }
            
            var toRemove = oldNodes.Where(oldNode => !newNodes.Contains(oldNode));
            var toAdd = newNodes.Where(newNode => !oldNodes.Contains(newNode));

            foreach (var node in toRemove)
            {
                node.RemoveEntity(moveEvent.Sender);
            }

            foreach (var node in toAdd)
            {
                node.AddEntity(moveEvent.Sender);
            }

            var newIndices = new Dictionary<GridId, List<MapIndices>>();
            foreach (var node in newNodes)
            {
                if (!newIndices.TryGetValue(node.ParentChunk.GridId, out var existing))
                {
                    existing = new List<MapIndices>();
                    newIndices[node.ParentChunk.GridId] = existing;
                }
                
                existing.Add(node.Indices);
            }

            _lastKnownNodes[moveEvent.Sender] = newNodes;
            EntityManager.EventBus.RaiseEvent(EventSource.Local, new TileLookupUpdateMessage(newIndices));
        }
    }
}
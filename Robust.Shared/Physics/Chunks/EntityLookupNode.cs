using System.Collections.Generic;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Maths;

namespace Robust.Shared.Physics.Chunks
{
    public sealed class EntityLookupNode
    {
        internal EntityLookupChunk ParentChunk { get; }

        internal Vector2i Indices { get; }

        internal IReadOnlySet<IEntity> Entities => _entities;

        private readonly HashSet<IEntity> _entities = new();

        internal EntityLookupNode(EntityLookupChunk parentChunk, Vector2i indices)
        {
            ParentChunk = parentChunk;
            Indices = indices;
        }

        internal void AddEntity(IEntity entity)
        {
            if (_entities.Add(entity))
                ParentChunk.EntityCount += 1;
        }

        internal void RemoveEntity(IEntity entity)
        {
            if (_entities.Remove(entity))
                ParentChunk.EntityCount -= 1;
        }
    }
}

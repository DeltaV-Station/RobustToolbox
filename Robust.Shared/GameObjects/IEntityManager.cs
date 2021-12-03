using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Prometheus;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Robust.Shared.GameObjects
{
    /// <summary>
    ///     Holds a collection of entities and the components attached to them.
    /// </summary>
    [PublicAPI]
    public partial interface IEntityManager
    {
        /// <summary>
        ///     The current simulation tick being processed.
        /// </summary>
        GameTick CurrentTick { get; }

        void Initialize();
        void Startup();
        void Shutdown();

        /// <summary>
        ///     Drops every entity, component and entity system.
        /// </summary>
        void Cleanup();
        void TickUpdate(float frameTime, Histogram? histogram=null);

        /// <summary>
        ///     Client-specific per-render frame updating.
        /// </summary>
        void FrameUpdate(float frameTime);

        IComponentFactory ComponentFactory { get; }
        IEntitySystemManager EntitySysManager { get; }
        IEntityNetworkManager? EntityNetManager { get; }
        IEventBus EventBus { get; }

        #region Entity Management

        event EventHandler<EntityUid>? EntityAdded;
        event EventHandler<EntityUid>? EntityInitialized;
        event EventHandler<EntityUid>? EntityStarted;
        event EventHandler<EntityUid>? EntityDeleted;

        IEntity CreateEntityUninitialized(string? prototypeName, EntityUid? euid);

        IEntity CreateEntityUninitialized(string? prototypeName);

        IEntity CreateEntityUninitialized(string? prototypeName, EntityCoordinates coordinates);

        IEntity CreateEntityUninitialized(string? prototypeName, MapCoordinates coordinates);

        /// <summary>
        /// Spawns an initialized entity at the default location, using the given prototype.
        /// </summary>
        /// <param name="protoName">The prototype to clone. If this is null, the entity won't have a prototype.</param>
        /// <param name="coordinates"></param>
        /// <returns>Newly created entity.</returns>
        IEntity SpawnEntity(string? protoName, EntityCoordinates coordinates);

        /// <summary>
        /// Spawns an entity at a specific position
        /// </summary>
        /// <param name="protoName"></param>
        /// <param name="coordinates"></param>
        /// <returns></returns>
        IEntity SpawnEntity(string? protoName, MapCoordinates coordinates);

        /// <summary>
        /// Returns an entity by id
        /// </summary>
        /// <param name="uid"></param>
        /// <returns>Entity or throws if entity id doesn't exist</returns>
        IEntity GetEntity(EntityUid uid);

        /// <summary>
        /// Attempt to get an entity, returning whether or not an entity was gotten.
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="entity">The requested entity or null if the entity couldn't be found.</param>
        /// <returns>True if a value was returned, false otherwise.</returns>
        bool TryGetEntity(EntityUid uid, [NotNullWhen(true)] out IEntity? entity);

        /// <summary>
        /// How many entities are currently active.
        /// </summary>
        int EntityCount { get; }

        /// <summary>
        /// Returns all entities
        /// </summary>
        /// <returns></returns>
        IEnumerable<IEntity> GetEntities();

        /// <summary>
        /// Returns all entities by uid
        /// </summary>
        /// <returns></returns>
        IEnumerable<EntityUid> GetEntityUids();

        public void DirtyEntity(EntityUid uid);

        public void QueueDeleteEntity(IEntity entity);

        public void QueueDeleteEntity(EntityUid uid);

        /// <summary>
        /// Shuts-down and removes given <see cref="IEntity"/>. This is also broadcast to all clients.
        /// </summary>
        /// <param name="e">Entity to remove</param>
        void DeleteEntity(IEntity e);

        /// <summary>
        /// Shuts-down and removes the entity with the given <see cref="EntityUid"/>. This is also broadcast to all clients.
        /// </summary>
        /// <param name="uid">Uid of entity to remove.</param>
        void DeleteEntity(EntityUid uid);

        /// <summary>
        /// Checks whether an entity with the specified ID exists.
        /// </summary>
        bool EntityExists(EntityUid uid);

        /// <summary>
        /// Returns a string representation of an entity with various information regarding it.
        /// </summary>
        EntityStringRepresentation ToPrettyString(EntityUid uid);

        #endregion Entity Management
    }
}

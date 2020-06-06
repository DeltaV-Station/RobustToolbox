﻿using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Systems;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.IoC;
using Robust.Shared.Reflection;

namespace Robust.Shared.GameObjects.Systems
{
    /// <summary>
    ///     A subsystem that acts on all components of a type at once.
    /// </summary>
    /// <remarks>
    ///     This class is instantiated by the <c>EntitySystemManager</c>, and any IoC Dependencies will be resolved.
    /// </remarks>
    [Reflect(false), PublicAPI]
    public abstract class EntitySystem : IEntitySystem
    {
        [Dependency] protected readonly IEntityManager EntityManager = default!;
        [Dependency] protected readonly IEntitySystemManager EntitySystemManager = default!;
        [Dependency] protected readonly IEntityNetworkManager EntityNetworkManager = default!;

        protected IEntityQuery? EntityQuery;
        protected IEnumerable<IEntity> RelevantEntities => EntityQuery != null ? EntityManager.GetEntities(EntityQuery) : EntityManager.GetEntities();

        /// <inheritdoc />
        public virtual void Initialize() { }

        /// <inheritdoc />
        public virtual void Update(float frameTime) { }

        /// <inheritdoc />
        public virtual void FrameUpdate(float frameTime) { }

        /// <inheritdoc />
        public virtual void Shutdown() { }


        #region Event Proxy

        protected void SubscribeNetworkEvent<T>(EntityEventHandler<T> handler)
            where T : EntitySystemMessage
        {
            EntityManager.EventBus.SubscribeEvent(EventSource.Network, this, handler);
        }

        protected void SubscribeNetworkEvent<T>(EntitySessionEventHandler<T> handler)
            where T : EntitySystemMessage
        {
            EntityManager.EventBus.SubscribeSessionEvent(EventSource.Network, this, handler);
        }

        protected void SubscribeLocalEvent<T>(EntityEventHandler<T> handler)
            where T : EntitySystemMessage
        {
            EntityManager.EventBus.SubscribeEvent(EventSource.Local, this, handler);
        }

        protected void SubscribeLocalEvent<T>(EntitySessionEventHandler<T> handler)
            where T : EntitySystemMessage
        {
            EntityManager.EventBus.SubscribeSessionEvent(EventSource.Local, this, handler);
        }

        protected void UnsubscribeNetworkEvent<T>()
            where T : EntitySystemMessage
        {
            EntityManager.EventBus.UnsubscribeEvent<T>(EventSource.Network, this);
        }

        protected void UnsubscribeLocalEvent<T>()
            where T : EntitySystemMessage
        {
            EntityManager.EventBus.UnsubscribeEvent<T>(EventSource.Local, this);
        }

        protected void RaiseLocalEvent(EntitySystemMessage message)
        {
            EntityManager.EventBus.RaiseEvent(EventSource.Local, message);
        }

        protected void QueueLocalEvent(EntitySystemMessage message)
        {
            EntityManager.EventBus.QueueEvent(EventSource.Local, message);
        }

        protected void RaiseNetworkEvent(EntitySystemMessage message)
        {
            EntityNetworkManager.SendSystemNetworkMessage(message);
        }

        protected void RaiseNetworkEvent(EntitySystemMessage message, INetChannel channel)
        {
            EntityNetworkManager.SendSystemNetworkMessage(message, channel);
        }

        protected Task<T> AwaitNetworkEvent<T>(CancellationToken cancellationToken)
            where T : EntitySystemMessage
        {
            return EntityManager.EventBus.AwaitEvent<T>(EventSource.Network, cancellationToken);
        }

        #endregion

        #region Static Helpers
        /*
         NOTE: Static helpers relating to EntitySystems are here rather than in a
         static helper class for conciseness / usability. If we had an "EntitySystems" static class
         it would conflict with any imported namespace called "EntitySystems" and require using alias directive, and
         if we called it something longer like "EntitySystemUtility", writing out "EntitySystemUtility.Get" seems
         pretty tedious for a potentially commonly-used method. Putting it here allows writing "EntitySystem.Get"
         which is nice and concise.
         */

        /// <summary>
        /// Gets the indicated entity system.
        /// </summary>
        /// <typeparam name="T">entity system to get</typeparam>
        /// <returns></returns>
        public static T Get<T>() where T : IEntitySystem
        {
            return IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<T>();
        }

        /// <summary>
        /// Tries to get an entity system of the specified type.
        /// </summary>
        /// <typeparam name="T">Type of entity system to find.</typeparam>
        /// <param name="entitySystem">instance matching the specified type (if exists).</param>
        /// <returns>If an instance of the specified entity system type exists.</returns>
        public static bool TryGet<T>(out T entitySystem) where T : IEntitySystem
        {
            return IoCManager.Resolve<IEntitySystemManager>().TryGetEntitySystem<T>(out entitySystem);
        }

        #endregion
    }
}

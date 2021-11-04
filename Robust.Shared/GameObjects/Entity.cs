using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Robust.Shared.GameObjects
{
    /// <inheritdoc />
    public sealed class Entity : IEntity
    {
        #region Members

        /// <inheritdoc />
        public IEntityManager EntityManager { get; }

        /// <inheritdoc />
        [ViewVariables]
        public EntityUid Uid { get; }

        /// <inheritdoc />
        EntityLifeStage IEntity.LifeStage { get => LifeStage; set => LifeStage = value; }

        public EntityLifeStage LifeStage { get => MetaData.EntityLifeStage; internal set => MetaData.EntityLifeStage = value; }

        /// <inheritdoc />
        [ViewVariables]
        public EntityPrototype? Prototype
        {
            get => MetaData.EntityPrototype;
            internal set => MetaData.EntityPrototype = value;
        }

        /// <inheritdoc />
        [ViewVariables(VVAccess.ReadWrite)]
        public string Description
        {
            get => MetaData.EntityDescription;
            set => MetaData.EntityDescription = value;
        }

        /// <inheritdoc />
        [ViewVariables]
        // Every entity starts at tick 1, because they are conceptually created in the time between 0->1
        GameTick IEntity.LastModifiedTick { get; set; } = new(1);

        /// <inheritdoc />
        [ViewVariables(VVAccess.ReadWrite)]
        public string Name
        {
            get => MetaData.EntityName;
            set => MetaData.EntityName = value;
        }

        /// <inheritdoc />
        public bool Initialized => LifeStage >= EntityLifeStage.Initialized;

        /// <inheritdoc />
        public bool Initializing => LifeStage == EntityLifeStage.Initializing;

        /// <inheritdoc />
        public bool Deleted => LifeStage >= EntityLifeStage.Deleted;

        [ViewVariables]
        public bool Paused { get => MetaData.EntityPaused; set => MetaData.EntityPaused = value; }

        private ITransformComponent? _transform;

        /// <inheritdoc />
        [ViewVariables]
        public ITransformComponent Transform => _transform ??= GetComponent<ITransformComponent>();

        private MetaDataComponent? _metaData;

        /// <inheritdoc />
        [ViewVariables]
        public MetaDataComponent MetaData
        {
            get => _metaData ??= GetComponent<MetaDataComponent>();
            internal set => _metaData = value;
        }

        #endregion Members

        #region Initialization

        public Entity(IEntityManager entityManager, EntityUid uid)
        {
            EntityManager = entityManager;
            Uid = uid;
        }

        /// <inheritdoc />
        public bool IsValid()
        {
            return EntityManager.EntityExists(Uid);
        }

        /// <summary>
        ///     Calls Initialize() on all registered components.
        /// </summary>
        public void InitializeComponents()
        {
            // TODO: Move this to EntityManager.

            DebugTools.Assert(LifeStage == EntityLifeStage.PreInit);
            LifeStage = EntityLifeStage.Initializing;

            // Initialize() can modify the collection of components.
            var components = EntityManager.GetComponents(Uid)
                .OrderBy(x => x switch
                {
                    ITransformComponent _ => 0,
                    IPhysBody _ => 1,
                    _ => int.MaxValue
                });

            foreach (var component in components)
            {
                var comp = (Component) component;
                if (comp.Initialized)
                    continue;

                comp.LifeInitialize();
            }

#if DEBUG
            // Second integrity check in case of.
            foreach (var t in EntityManager.GetComponents(Uid))
            {
                if (!t.Initialized)
                {
                    DebugTools.Assert($"Component {t.Name} was not initialized at the end of {nameof(InitializeComponents)}.");
                }
            }

#endif
            DebugTools.Assert(LifeStage == EntityLifeStage.Initializing);
            LifeStage = EntityLifeStage.Initialized;
            EntityManager.EventBus.RaiseEvent(EventSource.Local, new EntityInitializedMessage(this));
        }

        /// <summary>
        ///     Calls Startup() on all registered components.
        /// </summary>
        public void StartAllComponents()
        {
            // TODO: Move this to EntityManager.
            // Startup() can modify _components
            // This code can only handle additions to the list. Is there a better way? Probably not.
            var comps = EntityManager.GetComponents(Uid)
                .OrderBy(x => x switch
                {
                    ITransformComponent _ => 0,
                    IPhysBody _ => 1,
                    _ => int.MaxValue
                });

            foreach (var component in comps)
            {
                var comp = (Component) component;
                if (comp.LifeStage == ComponentLifeStage.Initialized)
                {
                    comp.LifeStartup();
                }
            }
        }

        #endregion Initialization

        #region Component Messaging

        /// <inheritdoc />
        [Obsolete("Component Messages are deprecated, use Entity Events instead.")]
        public void SendMessage(IComponent? owner, ComponentMessage message)
        {
            var components = EntityManager.GetComponents(Uid);
            foreach (var component in components)
            {
                if (owner != component)
                    component.HandleMessage(message, owner);
            }
        }

        /// <inheritdoc />
        [Obsolete("Component Messages are deprecated, use Entity Events instead.")]
        public void SendNetworkMessage(IComponent owner, ComponentMessage message, INetChannel? channel = null)
        {
            EntityManager.EntityNetManager?.SendComponentNetworkMessage(channel, this, owner, message);
        }

        #endregion Component Messaging

        #region Components

        /// <summary>
        ///     Public method to add a component to an entity.
        ///     Calls the component's onAdd method, which also adds it to the component manager.
        /// </summary>
        /// <param name="component">The component to add.</param>
        public void AddComponent(Component component)
        {
            EntityManager.AddComponent(this, component);
        }

        /// <inheritdoc />
        public T AddComponent<T>()
            where T : Component, new()
        {
            return EntityManager.AddComponent<T>(this);
        }

        /// <inheritdoc />
        public void RemoveComponent<T>()
        {
            EntityManager.RemoveComponent<T>(Uid);
        }

        /// <inheritdoc />
        public bool HasComponent<T>()
        {
            return EntityManager.HasComponent<T>(Uid);
        }

        /// <inheritdoc />
        public bool HasComponent(Type type)
        {
            return EntityManager.HasComponent(Uid, type);
        }

        /// <inheritdoc />
        public T GetComponent<T>()
        {
            DebugTools.Assert(!Deleted, "Tried to get component on a deleted entity.");

            return (T)EntityManager.GetComponent(Uid, typeof(T));
        }

        /// <inheritdoc />
        public IComponent GetComponent(Type type)
        {
            DebugTools.Assert(!Deleted, "Tried to get component on a deleted entity.");

            return EntityManager.GetComponent(Uid, type);
        }

        /// <inheritdoc />
        public bool TryGetComponent<T>([NotNullWhen(true)] out T? component) where T : class
        {
            DebugTools.Assert(!Deleted, "Tried to get component on a deleted entity.");

            return EntityManager.TryGetComponent(Uid, out component);
        }

        public T? GetComponentOrNull<T>() where T : class
        {
            return TryGetComponent(out T? component) ? component : default;
        }

        /// <inheritdoc />
        public bool TryGetComponent(Type type, [NotNullWhen(true)] out IComponent? component)
        {
            DebugTools.Assert(!Deleted, "Tried to get component on a deleted entity.");

            return EntityManager.TryGetComponent(Uid, type, out component);
        }

        public IComponent? GetComponentOrNull(Type type)
        {
            return TryGetComponent(type, out var component) ? component : null;
        }

        /// <inheritdoc />
        public void QueueDelete()
        {
            EntityManager.QueueDeleteEntity(this);
        }

        /// <inheritdoc />
        public void Delete()
        {
            EntityManager.DeleteEntity(this);
        }

        /// <inheritdoc />
        public IEnumerable<IComponent> GetAllComponents()
        {
            return EntityManager.GetComponents(Uid);
        }

        /// <inheritdoc />
        public IEnumerable<T> GetAllComponents<T>()
        {
            return EntityManager.GetComponents<T>(Uid);
        }

        #endregion Components

        /// <inheritdoc />
        public override string ToString()
        {
            if (Deleted)
            {
                return $"{Name} ({Uid}, {Prototype?.ID})D";
            }
            return $"{Name} ({Uid}, {Prototype?.ID})";
        }
    }
}

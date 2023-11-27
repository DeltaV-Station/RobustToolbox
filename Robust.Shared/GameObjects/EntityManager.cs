using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Arch.Core;
using Arch.Core.Extensions;
using Arch.Core.Utils;
using Collections.Pooled;
using Prometheus;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Runtime.CompilerServices;
using ComponentRegistry = Robust.Shared.Prototypes.ComponentRegistry;

namespace Robust.Shared.GameObjects
{
    public delegate void EntityUidQueryCallback(EntityUid uid);

    public delegate void ComponentQueryCallback<T>(EntityUid uid, T component) where T : IComponent;

    /// <inheritdoc />
    [Virtual]
    public partial class EntityManager : IEntityManager
    {
        #region Dependencies

        [IoC.Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
        [IoC.Dependency] protected readonly ILogManager LogManager = default!;
        [IoC.Dependency] private readonly IEntitySystemManager _entitySystemManager = default!;
        [IoC.Dependency] private readonly IMapManager _mapManager = default!;
        [IoC.Dependency] private readonly IGameTiming _gameTiming = default!;
        [IoC.Dependency] private readonly ISerializationManager _serManager = default!;
        [IoC.Dependency] private readonly ProfManager _prof = default!;
        [IoC.Dependency] private readonly INetManager _netMan = default!;

        // I feel like PJB might shed me for putting a system dependency here, but its required for setting entity
        // positions on spawn....
        private SharedTransformSystem _xforms = default!;

        private QueryDescription _archMetaQuery = new QueryDescription().WithAll<MetaDataComponent>();

        public EntityQuery<MetaDataComponent> MetaQuery;
        public EntityQuery<TransformComponent> TransformQuery;

        #endregion Dependencies

        /// <inheritdoc />
        public GameTick CurrentTick => _gameTiming.CurTick;

        public static readonly MapInitEvent MapInitEventInstance = new();

        IComponentFactory IEntityManager.ComponentFactory => ComponentFactory;

        /// <inheritdoc />
        public IEntitySystemManager EntitySysManager => _entitySystemManager;

        /// <inheritdoc />
        public virtual IEntityNetworkManager? EntityNetManager => null;

        protected readonly Queue<EntityUid> QueuedDeletions = new();
        protected readonly HashSet<EntityUid> QueuedDeletionsSet = new();

        private EntityDiffContext _context = new();

        private EntityEventBus _eventBus = null!;

        protected int NextNetworkId = (int) NetEntity.First;

        /// <inheritdoc />
        public IEventBus EventBus => _eventBus;

        public event Action<EntityUid>? EntityAdded;
        public event Action<EntityUid>? EntityInitialized;
        public event Action<EntityUid, MetaDataComponent>? EntityDeleted;

        /// <summary>
        /// Raised when an entity is queued for deletion. Not raised if an entity is deleted.
        /// </summary>
        public event Action<EntityUid>? EntityQueueDeleted;
        public event Action<EntityUid>? EntityDirtied; // only raised after initialization

        private string _xformName = string.Empty;

        private ComponentRegistration _metaReg = default!;
        private ComponentRegistration _xformReg = default!;

        private SharedMapSystem _mapSystem = default!;

        private ISawmill _sawmill = default!;
        private ISawmill _resolveSawmill = default!;

        public bool Started { get; protected set; }

        public bool ShuttingDown { get; protected set; }

        public bool Initialized { get; protected set; }

        /// <summary>
        /// Constructs a new instance of <see cref="EntityManager"/>.
        /// </summary>
        public EntityManager()
        {
        }

        public virtual void Initialize()
        {
            if (Initialized)
                throw new InvalidOperationException("Initialize() called multiple times");

            _eventBus = new EntityEventBus(this);

            InitializeArch();
            _metaReg = _componentFactory.GetRegistration(typeof(MetaDataComponent));
            _xformReg = _componentFactory.GetRegistration(typeof(TransformComponent));
            _xformName = _xformReg.Name;
            _sawmill = LogManager.GetSawmill("entity");
            _resolveSawmill = LogManager.GetSawmill("resolve");

            Initialized = true;
        }

        /// <summary>
        /// Returns true if the entity's data (apart from transform) is default.
        /// </summary>
        public bool IsDefault(EntityUid uid)
        {
            if (!MetaQuery.TryGetComponent(uid, out var metadata) || metadata.EntityPrototype == null)
                return false;

            var prototype = metadata.EntityPrototype;

            // Check if entity name / description match
            if (metadata.EntityName != prototype.Name ||
                metadata.EntityDescription != prototype.Description)
            {
                return false;
            }

            var protoData = PrototypeManager.GetPrototypeData(prototype);
            var comps = _world.GetAllComponents(uid);

            // Fast check if the component counts match.
            // Note that transform and metadata are not included in the prototype data.
            if (protoData.Count + 2 != comps.Length)
                return false;

            foreach (var comp in comps)
            {
                var component = (IComponent)comp!;

                if (component.Deleted)
                    return false;

                var compType = component.GetType();
                var compName = _componentFactory.GetComponentName(compType);
                if (compName == _xformName || compName == _metaReg.Name)
                    continue;

                // If the component isn't on the prototype then it's custom.
                if (!protoData.TryGetValue(compName, out var protoMapping))
                    return false;

                MappingDataNode compMapping;
                try
                {
                    compMapping = _serManager.WriteValueAs<MappingDataNode>(compType, component, alwaysWrite: true, context: _context);
                }
                catch (Exception e)
                {
                    _sawmill.Error($"Failed to serialize {compName} component of entity prototype {prototype.ID}. Exception: {e.Message}");
                    return false;
                }

                var diff = compMapping.Except(protoMapping);

                if (diff != null && diff.Children.Count != 0)
                    return false;
            }

            return true;
        }

        public virtual void Startup()
        {
            if(!Initialized)
                throw new InvalidOperationException("Startup() called without Initialized");
            if (Started)
                throw new InvalidOperationException("Startup() called multiple times");

            // TODO: Probably better to call this on its own given it's so infrequent.
            _entitySystemManager.Initialize();
            Started = true;
            _eventBus.CalcOrdering();
            _mapSystem = System<SharedMapSystem>();
            _xforms = System<SharedTransformSystem>();
            MetaQuery = GetEntityQuery<MetaDataComponent>();
            TransformQuery = GetEntityQuery<TransformComponent>();
        }

        public virtual void Shutdown()
        {
            ShuttingDown = true;
            FlushEntities();
            _eventBus.ClearEventTables();
            _entitySystemManager.Shutdown();
            ShutdownArch();
            ClearComponents();
            ShuttingDown = false;
            Started = false;
        }

        public virtual void Cleanup()
        {
            ShuttingDown = true;
            FlushEntities();
            _entitySystemManager.Clear();
            _eventBus.Dispose();
            _eventBus = null!;
            ShutdownArch();
            ClearComponents();

            ShuttingDown = false;
            Initialized = false;
            Started = false;
        }

        public virtual void TickUpdate(float frameTime, bool noPredictions, Histogram? histogram)
        {
            using (histogram?.WithLabels("EntitySystems").NewTimer())
            using (_prof.Group("Systems"))
            {
                _entitySystemManager.TickUpdate(frameTime, noPredictions);
            }

            using (histogram?.WithLabels("EntityEventBus").NewTimer())
            using (_prof.Group("Events"))
            {
                _eventBus.ProcessEventQueue();
            }

            using (histogram?.WithLabels("QueuedDeletion").NewTimer())
            using (_prof.Group("QueueDel"))
            {
                while (QueuedDeletions.TryDequeue(out var uid))
                {
                    DeleteEntity(uid);
                }

                QueuedDeletionsSet.Clear();
            }

            using (histogram?.WithLabels("ComponentCull").NewTimer())
            using (_prof.Group("ComponentCull"))
            {
                CullRemovedComponents();
            }
        }

        public virtual void FrameUpdate(float frameTime)
        {
            _entitySystemManager.FrameUpdate(frameTime);
        }

        #region Entity Management

        public EntityUid CreateEntityUninitialized(string? prototypeName, EntityUid euid, ComponentRegistry? overrides = null)
        {
            return CreateEntity(prototypeName, out _, out _, overrides);
        }

        /// <inheritdoc />
        public virtual EntityUid CreateEntityUninitialized(string? prototypeName, ComponentRegistry? overrides = null)
        {
            return CreateEntity(prototypeName, out _, out _, overrides);
        }

        /// <inheritdoc />
        public virtual EntityUid CreateEntityUninitialized(string? prototypeName, EntityCoordinates coordinates, ComponentRegistry? overrides = null)
        {
            var newEntity = CreateEntity(prototypeName, out _, out var xform, overrides);
            _xforms.SetCoordinates(newEntity, xform, coordinates, unanchor: false);
            return newEntity;
        }

        /// <inheritdoc />
        public virtual EntityUid CreateEntityUninitialized(string? prototypeName, MapCoordinates coordinates, ComponentRegistry? overrides = null)
        {
            var newEntity = CreateEntity(prototypeName, out _, out var transform, overrides);

            if (coordinates.MapId == MapId.Nullspace)
            {
                DebugTools.Assert(_mapManager.GetMapEntityId(coordinates.MapId) == EntityUid.Invalid);
                transform._parent = EntityUid.Invalid;
                transform.Anchored = false;
                return newEntity;
            }

            var mapEnt = _mapManager.GetMapEntityId(coordinates.MapId);
            if (!TryGetComponent(mapEnt, out TransformComponent? mapXform))
                throw new ArgumentException($"Attempted to spawn entity on an invalid map. Coordinates: {coordinates}");

            EntityCoordinates coords;
            if (transform.Anchored && _mapManager.TryFindGridAt(coordinates, out var gridUid, out var grid))
            {
                coords = new EntityCoordinates(gridUid, _mapSystem.WorldToLocal(gridUid, grid, coordinates.Position));
                _xforms.SetCoordinates(newEntity, transform, coords, unanchor: false);
            }
            else
            {
                coords = new EntityCoordinates(mapEnt, coordinates.Position);
                _xforms.SetCoordinates(newEntity, transform, coords, null, newParent: mapXform);
            }

            return newEntity;
        }

        /// <inheritdoc />
        public int EntityCount => _world.Size;

        /// <inheritdoc />
        public IEnumerable<EntityUid> GetEntities()
        {
            var ents = new List<Entity>();
            _world.GetEntities(_archMetaQuery, ents);

            foreach (var entity in ents)
            {
                yield return EntityUid.FromArch(_world, entity);
            }
        }

        /// <inheritdoc />
        public virtual void DirtyEntity(EntityUid uid, MetaDataComponent? metadata = null)
        {
            // We want to retrieve MetaDataComponent even if its Deleted flag is set.
            if (!MetaQuery.ResolveInternal(uid, ref metadata))
                return;

            if (metadata.EntityLastModifiedTick == _gameTiming.CurTick)
                return;

            metadata.EntityLastModifiedTick = _gameTiming.CurTick;

            if (metadata.EntityLifeStage > EntityLifeStage.Initializing)
            {
                EntityDirtied?.Invoke(uid);
            }
        }

        /// <inheritdoc />
        [Obsolete("use override with an EntityUid")]
        public void Dirty(IComponent component, MetaDataComponent? meta = null)
        {
            Dirty(component.Owner, component, meta);
        }

        /// <inheritdoc />
        public virtual void Dirty(EntityUid uid, IComponent component, MetaDataComponent? meta = null)
        {
            Dirty(new Entity<IComponent>(uid, component), meta);
        }

        /// <inheritdoc />
        public virtual void Dirty<T>(Entity<T> ent, MetaDataComponent? meta = null) where T : IComponent
        {
            if (ent.Comp.LifeStage >= ComponentLifeStage.Removing || !ent.Comp.NetSyncEnabled)
                return;

            DebugTools.AssertOwner(ent, ent.Comp);
            DirtyEntity(ent, meta);
#pragma warning disable CS0618 // Type or member is obsolete
            ent.Comp.LastModifiedTick = CurrentTick;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Shuts-down and removes given Entity. This is also broadcast to all clients.
        /// </summary>
        /// <param name="e">Entity to remove</param>
        public virtual void DeleteEntity(EntityUid? uid)
        {
            if (uid == null || uid == EntityUid.Invalid)
                return;
            var e = uid.Value;

            // Some UIs get disposed after entity-manager has shut down and already deleted all entities.
            if (!Started)
                return;

            // Networking blindly spams entities at this function, they can already be
            // deleted from being a child of a previously deleted entity
            // TODO: Why does networking need to send deletes for child entities?
            if (!MetaQuery.TryGetComponent(e, out var meta) || meta.EntityDeleted)
                return;

            if (meta.EntityLifeStage == EntityLifeStage.Terminating)
            {
                var msg = $"Called Delete on an entity already being deleted. Entity: {ToPrettyString(e)}";
#if !EXCEPTION_TOLERANCE
                throw new InvalidOperationException(msg);
#else
                _sawmill.Error($"{msg}. Trace: {Environment.StackTrace}");
#endif
            }

            // Notify all entities they are being terminated prior to detaching & deleting
            var xform = TransformQuery.GetComponent(e);
            RecursiveFlagEntityTermination(e, meta, xform);

            TransformComponent? parentXform = null;
            if (xform.ParentUid.IsValid())
                TransformQuery.Resolve(xform.ParentUid, ref parentXform);

            // Then actually delete them
            RecursiveDeleteEntity(e, meta, xform, parentXform);
        }

        private void RecursiveFlagEntityTermination(
            EntityUid uid,
            MetaDataComponent metadata,
            TransformComponent transform)
        {
            metadata.EntityLifeStage = EntityLifeStage.Terminating;

            try
            {
                var ev = new EntityTerminatingEvent(uid, metadata);
                EventBus.RaiseLocalEvent(uid, ref ev, true);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Caught exception while raising event {nameof(EntityTerminatingEvent)} on entity {ToPrettyString(uid, metadata)}\n{e}");
            }

            foreach (var child in transform._children)
            {
                if (!MetaQuery.TryGetComponent(child, out var childMeta) || childMeta.EntityDeleted)
                {
                    _sawmill.Error($"A deleted entity was still the transform child of another entity. Parent: {ToPrettyString(uid, metadata)}.");
                    transform._children.Remove(child);
                    continue;
                }

                RecursiveFlagEntityTermination(child, childMeta, TransformQuery.GetComponent(child));
            }
        }

        private void RecursiveDeleteEntity(
            EntityUid uid,
            MetaDataComponent metadata,
            TransformComponent transform,
            TransformComponent? parentXform)
        {
            DebugTools.Assert(transform.ParentUid.IsValid() == (parentXform != null));
            DebugTools.Assert(parentXform == null || parentXform.ChildEntities.Contains(uid));

            // Note about this method: #if EXCEPTION_TOLERANCE is not used here because we're gonna it in the future...

            // Detach the base entity to null before iterating over children
            // This also ensures that the entity-lookup updates don't have to be re-run for every child (which recurses up the transform hierarchy).
            if (transform.ParentUid != EntityUid.Invalid)
            {
                try
                {
                    _xforms.DetachParentToNull(uid, transform, parentXform);
                }
                catch (Exception e)
                {
                    _sawmill.Error($"Caught exception while trying to detach parent of entity '{ToPrettyString(uid, metadata)}' to null.\n{e}");
                }
            }

            foreach (var child in transform._children)
            {
                try
                {
                    var childMeta = MetaQuery.GetComponent(child);
                    var childXform = TransformQuery.GetComponent(child);
                    DebugTools.AssertEqual(childXform.ParentUid, uid);
                    RecursiveDeleteEntity(child, childMeta, childXform, transform);
                }
                catch(Exception e)
                {
                    _sawmill.Error($"Caught exception while trying to recursively delete child entity '{ToPrettyString(child)}' of '{ToPrettyString(uid, metadata)}'\n{e}");
                }
            }

            if (transform._children.Count != 0)
                _sawmill.Error($"Failed to delete all children of entity: {ToPrettyString(uid)}");

            // Shut down all components.
            var objComps = _world.GetAllComponents(uid);

            foreach (var comp in objComps)
            {
                var component = (IComponent)comp!;

                if (component.Running)
                {
                    try
                    {
                        LifeShutdown(component);
                    }
                    catch (Exception e)
                    {
                        _sawmill.Error($"Caught exception while trying to call shutdown on component of entity '{ToPrettyString(uid, metadata)}'\n{e}");
                    }
                }
            }

            // Dispose all my components, in a safe order so transform is available
            DisposeComponents(uid, metadata);
            metadata.EntityLifeStage = EntityLifeStage.Deleted;

            try
            {
                EntityDeleted?.Invoke(uid, metadata);
            }
            catch (Exception e)
            {
                _sawmill.Error($"Caught exception while invoking event {nameof(EntityDeleted)} on '{ToPrettyString(uid, metadata)}'\n{e}");
            }

            _eventBus.OnEntityDeleted(uid);
            DestroyArch(uid);
            // Need to get the ID above before MetadataComponent shutdown but only remove it after everything else is done.
            NetEntityLookup.Remove(metadata.NetEntity);
        }

        public virtual void QueueDeleteEntity(EntityUid? uid)
        {
            if (uid == null || uid.Value == EntityUid.Invalid)
                return;

            if (!QueuedDeletionsSet.Add(uid.Value))
                return;

            QueuedDeletions.Enqueue(uid.Value);
            EntityQueueDeleted?.Invoke(uid.Value);
        }

        public bool IsQueuedForDeletion(EntityUid uid) => QueuedDeletionsSet.Contains(uid);

        public bool EntityExists(EntityUid uid)
        {
            return IsAlive(uid);
        }

        public bool EntityExists(EntityUid? uid)
        {
            return uid.HasValue && EntityExists(uid.Value);
        }

        /// <inheritdoc />
        public bool IsPaused(EntityUid? uid, MetaDataComponent? metadata = null)
        {
            if (uid == null)
                return false;

            return MetaQuery.Resolve(uid.Value, ref metadata) && metadata.EntityPaused;
        }

        public bool Deleted(EntityUid uid)
        {
            return !IsAlive(uid) || !_world.TryGet(uid, out MetaDataComponent? comp) || comp!.EntityLifeStage > EntityLifeStage.Terminating;
        }

        /// <summary>
        /// Returns whether the entity is alive inside of the ECS world.
        /// </summary>
        internal bool IsAlive(EntityUid uid)
        {
            return ((EntityReference) uid).IsAlive(_world);
        }

        internal bool TryAlive(EntityUid uid, out EntityReference entity)
        {
            entity = uid;
            return entity.IsAlive(_world);
        }

        public bool Deleted([NotNullWhen(false)] EntityUid? uid)
        {
            return !uid.HasValue || Deleted(uid.Value);
        }

        /// <summary>
        /// Disposes all entities and clears all lists.
        /// </summary>
        public virtual void FlushEntities()
        {
            QueuedDeletions.Clear();
            QueuedDeletionsSet.Clear();
            foreach (var e in GetEntities().ToArray())
            {
                DeleteEntity(e);
            }

            // Arch bug atm
            // CleanupArch();

            if (_world.Size > 0)
                _sawmill.Error("Entities were spawned while flushing entities.");
        }

        /// <summary>
        ///     Allocates an entity and stores it but does not load components or do initialization.
        /// </summary>
        private protected EntityUid AllocEntity(
            EntityPrototype? prototype,
            out MetaDataComponent metadata,
            out TransformComponent xform)
        {
            var entity = AllocEntity(out metadata, out xform);
            metadata._entityPrototype = prototype;
            Dirty(entity, metadata, metadata);
            return entity;
        }

        /// <summary>
        ///     Allocates an entity and stores it but does not load components or do initialization.
        /// </summary>
        private EntityUid AllocEntity(out MetaDataComponent metadata, out TransformComponent xform)
        {
            SpawnEntityArch(out var uid);

            // we want this called before adding components
            EntityAdded?.Invoke(uid);
            _eventBus.OnEntityAdded(uid);
            var netEntity = GenerateNetEntity();

            metadata = new MetaDataComponent
            {
#pragma warning disable CS0618
                Owner = uid,
#pragma warning restore CS0618
                EntityLastModifiedTick = _gameTiming.CurTick
            };

            SetNetEntity(uid, netEntity, metadata);

            // add the required MetaDataComponent directly.
            AddComponentInternal(uid, metadata, _metaReg, true, metadata);

            // allocate the required TransformComponent
            xform = Unsafe.As<TransformComponent>(_componentFactory.GetComponent(_xformReg));
#pragma warning disable CS0618 // Type or member is obsolete
            xform.Owner = uid;
#pragma warning restore CS0618 // Type or member is obsolete
            AddComponentInternal(uid, xform, true, metadata);

            return uid;
        }

        /// <summary>
        ///     Allocates an entity and loads components but does not do initialization.
        /// </summary>
        private protected virtual EntityUid CreateEntity(string? prototypeName, out MetaDataComponent metadata, out TransformComponent xform, IEntityLoadContext? context = null)
        {
            if (prototypeName == null)
                return AllocEntity(out metadata, out xform);

            if (!PrototypeManager.TryIndex<EntityPrototype>(prototypeName, out var prototype))
                throw new EntityCreationException($"Attempted to spawn an entity with an invalid prototype: {prototypeName}");

            return CreateEntity(prototype, out metadata, out xform, context);
        }

        /// <summary>
        ///     Allocates an entity and loads components but does not do initialization.
        /// </summary>
        private protected EntityUid CreateEntity(EntityPrototype prototype, out MetaDataComponent metadata, out TransformComponent xform, IEntityLoadContext? context = null)
        {
            var entity = AllocEntity(prototype, out metadata, out xform);
            try
            {
                LoadEntity(metadata.EntityPrototype, entity, context);
                return entity;
            }
            catch (Exception e)
            {
                // Exception during entity loading.
                // Need to delete the entity to avoid corrupt state causing crashes later.
                DeleteEntity(entity);
                throw new EntityCreationException($"Exception inside CreateEntity with prototype {prototype.ID}", e);
            }
        }

        private protected void LoadEntity(EntityUid entity, IEntityLoadContext? context)
        {
            LoadEntity(MetaQuery.GetComponent(entity).EntityPrototype, entity, context);
        }

        private protected void LoadEntity(EntityUid entity, IEntityLoadContext? context, EntityPrototype? prototype)
        {
            LoadEntity(prototype, entity, context);
        }

        internal void LoadEntity(EntityPrototype? prototype, EntityUid entity, IEntityLoadContext? context)
        {
            var count = prototype?.Components.Count ?? 2;
            // Lort forgiv
            using var types = new PooledList<ComponentType>(count);
            using var comps = new PooledList<IComponent>(count);
            using var compRegs = new PooledList<ComponentRegistration>(count);
            Archetype arc;
            var metadata = MetaQuery.GetComponent(entity);

#if DEBUG
            arc = _world.GetArchetype(entity);
#endif

            if (prototype != null)
            {
                foreach (var (name, entry) in prototype.Components)
                {
                    if (context != null && context.ShouldSkipComponent(name))
                        continue;

                    var fullData = context != null && context.TryGetComponent(name, out var data) ? data : entry.Component;

                    var comp = EntityPrototype.EnsureCompExistsAndDeserialize(entity, _componentFactory, this, _serManager, name, fullData, context as ISerializationContext, metadata);
                    var compType = comp.CompReg.Idx.Type;

                    // Don't double add an existing component, just set data above.
                    if (!comp.Add)
                    {
                        continue;
                    }

                    types.Add(compType);
                    comps.Add(comp.Comp);
                    compRegs.Add(comp.CompReg);
                }
            }

            if (context != null)
            {
                foreach (var name in context.GetExtraComponentTypes())
                {
                    if (prototype != null && prototype.Components.ContainsKey(name))
                    {
                        // This component also exists in the prototype.
                        // This means that the previous step already caught both the prototype data AND map data.
                        // Meaning that re-running EnsureCompExistsAndDeserialize would wipe prototype data.
                        continue;
                    }

                    if (!context.TryGetComponent(name, out var data))
                    {
                        throw new InvalidOperationException(
                            $"{nameof(IEntityLoadContext)} provided component name {name} but refused to provide data");
                    }

                    var comp = EntityPrototype.EnsureCompExistsAndDeserialize(entity, _componentFactory, this, _serManager, name, data, context as ISerializationContext, metadata);
                    var compType = comp.CompReg.Idx.Type;

                    // Don't double add an existing component, just set data above.
                    if (!comp.Add)
                    {
                        continue;
                    }

                    types.Add(compType);
                    comps.Add(comp.Comp);
                    compRegs.Add(comp.CompReg);
                }
            }

            // Shouldn't be changing archetype above or we're having a bad time.
            DebugTools.Assert(_world.GetArchetype(entity).Equals(arc));

            // Yeah it can happen.
            if (types.Count == 0)
                return;

            _world.AddRange(entity, types);

            for (var i = 0; i < comps.Count; i++)
            {
                AddComponentInternal(entity, comps[i], compRegs[i], true, metadata: metadata);
            }
        }

        public void InitializeAndStartEntity(EntityUid entity, MapId? mapId = null)
        {
            try
            {
                // TODO: Pass this + transformcomp around
                var meta = MetaQuery.GetComponent(entity);
                InitializeEntity(entity, meta);
                StartEntity(entity);

                // If the map we're initializing the entity on is initialized, run map init on it.
                if (_mapManager.IsMapInitialized(mapId ?? TransformQuery.GetComponent(entity).MapID))
                    RunMapInit(entity, meta);
            }
            catch (Exception e)
            {
                DeleteEntity(entity);
                throw new EntityCreationException("Exception inside InitializeAndStartEntity", e);
            }
        }

        public void InitializeEntity(EntityUid entity, MetaDataComponent? meta = null)
        {
            InitializeComponents(entity, meta);
            EntityInitialized?.Invoke(entity);
        }

        public void StartEntity(EntityUid entity)
        {
            StartComponents(entity);
        }

        public void RunMapInit(EntityUid entity, MetaDataComponent meta)
        {
            if (meta.EntityLifeStage == EntityLifeStage.MapInitialized)
                return; // Already map initialized, do nothing.

            DebugTools.Assert(meta.EntityLifeStage == EntityLifeStage.Initialized, $"Expected entity {ToPrettyString(entity)} to be initialized, was {meta.EntityLifeStage}");
            meta.EntityLifeStage = EntityLifeStage.MapInitialized;

            EventBus.RaiseLocalEvent(entity, MapInitEventInstance, false);
        }

        /// <inheritdoc />
        [return: NotNullIfNotNull("uid")]
        public EntityStringRepresentation? ToPrettyString(EntityUid? uid, MetaDataComponent? metadata = null)
        {
            return uid == null ? null : ToPrettyString(uid.Value, metadata);
        }

        /// <inheritdoc />
        public virtual EntityStringRepresentation ToPrettyString(EntityUid uid, MetaDataComponent? metadata = null)
        {
            if (!MetaQuery.Resolve(uid, ref metadata, false))
                return new EntityStringRepresentation(uid, true);

            return new EntityStringRepresentation(uid, metadata.EntityDeleted, metadata.EntityName, metadata.EntityPrototype?.ID);
        }

        /// <inheritdoc />
        [return: NotNullIfNotNull("netEntity")]
        public EntityStringRepresentation? ToPrettyString(NetEntity? netEntity)
        {
            return netEntity == null ? null : ToPrettyString(netEntity.Value);
        }

        /// <inheritdoc />
        public EntityStringRepresentation ToPrettyString(NetEntity netEntity)
        {
            if (!TryGetEntityData(netEntity, out var uid, out var meta))
                return new EntityStringRepresentation(EntityUid.Invalid, true);

            return ToPrettyString(uid.Value, meta);
        }

        #endregion Entity Management

        public virtual void RaisePredictiveEvent<T>(T msg) where T : EntityEventArgs
        {
            // Part of shared the EntityManager so that systems can have convenient proxy methods, but the
            // server should never be calling this.
            DebugTools.Assert("Why are you raising predictive events on the server?");
        }

        /// <summary>
        /// Generates a unique network id and increments <see cref="NextNetworkId"/>
        /// </summary>
        protected virtual NetEntity GenerateNetEntity() => new(NextNetworkId++);
    }

    public enum EntityMessageType : byte
    {
        Error = 0,
        SystemMessage
    }
}

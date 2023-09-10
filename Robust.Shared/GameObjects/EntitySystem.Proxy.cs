using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Robust.Shared.GameObjects;

public partial class EntitySystem
{
    #region Entity LifeStage

    /// <inheritdoc cref="IEntityManager.EntityExists(EntityUid)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool Exists(EntityUid uid)
    {
        return EntityManager.EntityExists(uid);
    }

    /// <inheritdoc cref="IEntityManager.EntityExists(EntityUid?)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool Exists([NotNullWhen(true)] EntityUid? uid)
    {
        return EntityManager.EntityExists(uid);
    }

    /// <summary>
    ///     Retrieves whether the entity is initializing. Throws if the entity does not exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool Initializing(EntityUid uid, MetaDataComponent? metaData = null)
    {
        return LifeStage(uid, metaData) == EntityLifeStage.Initializing;
    }

    /// <summary>
    ///     Retrieves whether the entity is initialized. Throws if the entity does not exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool Initialized(EntityUid uid, MetaDataComponent? metaData = null)
    {
        return LifeStage(uid, metaData) >= EntityLifeStage.Initialized;
    }

    /// <summary>
    ///     Retrieves whether the entity is being terminated. Throws if the entity does not exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool Terminating(EntityUid uid, MetaDataComponent? metaData = null)
    {
        return LifeStage(uid, metaData) == EntityLifeStage.Terminating;
    }

    /// <summary>
    ///     Retrieves whether the entity is deleted or is nonexistent.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool Deleted(EntityUid uid, MetaDataComponent? metaData = null)
    {
        if (!Resolve(uid, ref metaData, false))
            return true;

        return metaData.EntityDeleted;
    }

    /// <summary>
    ///     Checks whether the entity is being or has been deleted (or never existed in the first place).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TerminatingOrDeleted(EntityUid uid, MetaDataComponent? metaData = null)
    {
        if (!Resolve(uid, ref metaData, false))
            return true;

        return metaData.EntityLifeStage >= EntityLifeStage.Terminating;
    }

    /// <summary>
    ///     Retrieves whether the entity is deleted or is nonexistent.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool Deleted(EntityUid uid, EntityQuery<MetaDataComponent> metaQuery)
    {
        if (!metaQuery.TryGetComponent(uid, out var meta))
            return true;

        return meta.EntityDeleted;
    }

    /// <summary>
    ///     Retrieves whether the entity is deleted or is nonexistent.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool Deleted([NotNullWhen(false)] EntityUid? uid)
    {
        return !uid.HasValue || Deleted(uid.Value);
    }

    /// <inheritdoc cref="MetaDataComponent.EntityLifeStage" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityLifeStage LifeStage(EntityUid uid, MetaDataComponent? metaData = null)
    {
        if (!Resolve(uid, ref metaData, false))
            throw CompNotFound<MetaDataComponent>(uid);

        return metaData.EntityLifeStage;
    }

    /// <summary>
    ///     Attempts to retrieve whether the entity is initializing.
    /// </summary>
    /// <returns>Whether it could be retrieved.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryInitializing(EntityUid uid, [NotNullWhen(true)] out bool? initializing, MetaDataComponent? metaData = null)
    {
        if (!TryLifeStage(uid, out var lifeStage, metaData))
        {
            initializing = null;
            return false;
        }

        initializing = lifeStage == EntityLifeStage.Initializing;
        return true;
    }

    /// <summary>
    ///     Attempts to retrieve whether the entity is initialized.
    /// </summary>
    /// <returns>Whether it could be retrieved.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryInitialized(EntityUid uid, [NotNullWhen(true)] out bool? initialized, MetaDataComponent? metaData = null)
    {
        if (!TryLifeStage(uid, out var lifeStage, metaData))
        {
            initialized = null;
            return false;
        }

        initialized = lifeStage >= EntityLifeStage.Initialized;
        return true;
    }

    /// <summary>
    ///     Attempts to retrieve whether the entity is terminating.
    /// </summary>
    /// <returns>Whether it could be retrieved.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryTerminating(EntityUid uid, [NotNullWhen(true)] out bool? terminating, MetaDataComponent? metaData = null)
    {
        if (!TryLifeStage(uid, out var lifeStage, metaData))
        {
            terminating = null;
            return false;
        }

        terminating = lifeStage == EntityLifeStage.Terminating;
        return true;
    }

    /// <summary>
    ///     Attempts to retrieve the life-stage of the entity.
    ///     <seealso cref="MetaDataComponent.EntityLifeStage"/>
    /// </summary>
    /// <returns>Whether it could be retrieved.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryLifeStage(EntityUid uid, [NotNullWhen(true)] out EntityLifeStage? lifeStage, MetaDataComponent? metaData = null)
    {
        if (!Resolve(uid, ref metaData))
        {
            lifeStage = null;
            return false;
        }

        lifeStage = metaData.EntityLifeStage;
        return true;
    }

    #endregion

    #region Entity Metadata

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool IsPaused(EntityUid? uid, MetaDataComponent? metadata = null)
    {
        return EntityManager.IsPaused(uid, metadata);
    }

    /// <summary>
    /// Marks this entity as dirty so that it will be updated over the network.
    /// </summary>
    /// <remarks>
    /// Calling Dirty on a component will call this directly.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void DirtyEntity(EntityUid uid, MetaDataComponent? meta = null)
    {
        EntityManager.DirtyEntity(uid, meta);
    }

    /// <summary>
    ///     Marks a component as dirty. This also implicitly dirties the entity this component belongs to.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Obsolete("Use Dirty(EntityUid, Component, MetaDataComponent?")]
    protected void Dirty(Component component, MetaDataComponent? meta = null)
    {
        EntityManager.Dirty(component, meta);
    }

    /// <summary>
    ///     Marks a component as dirty. This also implicitly dirties the entity this component belongs to.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Dirty(EntityUid uid, Component component, MetaDataComponent? meta = null)
    {
        EntityManager.Dirty(uid, component, meta);
    }

    /// <summary>
    ///     Retrieves the name of an entity.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the entity doesn't exist.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected string Name(EntityUid uid, MetaDataComponent? metaData = null)
    {
        if(!Resolve(uid, ref metaData, false))
            throw CompNotFound<MetaDataComponent>(uid);

        return metaData.EntityName;
    }

    /// <summary>
    ///     Retrieves the description of an entity.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the entity doesn't exist.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected string Description(EntityUid uid, MetaDataComponent? metaData = null)
    {
        if(!Resolve(uid, ref metaData, false))
            throw CompNotFound<MetaDataComponent>(uid);

        return metaData.EntityDescription;
    }

    /// <summary>
    ///     Retrieves the prototype of an entity, if any.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the entity doesn't exist.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityPrototype? Prototype(EntityUid uid, MetaDataComponent? metaData = null)
    {
        if (!Resolve(uid, ref metaData, false))
            throw CompNotFound<MetaDataComponent>(uid);

        return metaData.EntityPrototype;
    }

    /// <summary>
    ///     Retrieves the last <see cref="GameTick"/> the entity was modified at.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the entity doesn't exist.</exception>
    protected GameTick LastModifiedTick(EntityUid uid, MetaDataComponent? metaData = null)
    {
        if (!Resolve(uid, ref metaData, false))
            throw CompNotFound<MetaDataComponent>(uid);

        return metaData.EntityLastModifiedTick;
    }

    /// <summary>
    ///     Retrieves whether the entity is paused or not.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the entity doesn't exist.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool Paused(EntityUid uid, MetaDataComponent? metaData = null)
    {
        if (!Resolve(uid, ref metaData, false))
            throw CompNotFound<MetaDataComponent>(uid);

        return metaData.EntityPaused;
    }

    /// <summary>
    ///     Sets the paused status on an entity.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the entity doesn't exist.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void SetPaused(EntityUid uid, bool paused, MetaDataComponent? metaData = null)
    {
        if (!Resolve(uid, ref metaData, false))
            throw CompNotFound<MetaDataComponent>(uid);

        EntityManager.EntitySysManager.GetEntitySystem<MetaDataSystem>().SetEntityPaused(uid, paused, metaData);
    }

    /// <summary>
    ///     Attempts to mark an entity as dirty.
    /// </summary>
    /// <returns>Whether the operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryDirty(EntityUid uid)
    {
        if (!Exists(uid))
            return false;

        DirtyEntity(uid);
        return true;
    }

    /// <summary>
    ///     Attempts to retrieve the name of an entity.
    /// </summary>
    /// <returns>Whether the name could be retrieved.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryName(EntityUid uid, [NotNullWhen(true)] out string? name, MetaDataComponent? metaData = null)
    {
        if (!Resolve(uid, ref metaData, false))
        {
            name = null;
            return false;
        }

        name = metaData.EntityName;
        return true;
    }

    /// <summary>
    ///     Attempts to retrieve the description of an entity.
    /// </summary>
    /// <returns>Whether the description could be retrieved.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryDescription(EntityUid uid, [NotNullWhen(true)] out string? description, MetaDataComponent? metaData = null)
    {
        if (!Resolve(uid, ref metaData, false))
        {
            description = null;
            return false;
        }

        description = metaData.EntityDescription;
        return true;
    }

    /// <summary>
    ///     Attempts to retrieve the prototype of an entity.
    /// </summary>
    /// <returns>Whether the prototype could be retrieved and was not null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryPrototype(EntityUid uid, [NotNullWhen(true)] out EntityPrototype? prototype, MetaDataComponent? metaData = null)
    {
        if (!Resolve(uid, ref metaData, false))
        {
            prototype = null;
            return false;
        }

        prototype = metaData.EntityPrototype;
        return prototype != null;
    }

    /// <summary>
    ///     Attempts to retrieve the last <see cref="GameTick"/> the entity was modified at.
    /// </summary>
    /// <returns>Whether the last modified tick could be retrieved.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryLastModifiedTick(EntityUid uid, [NotNullWhen(true)] out GameTick? lastModifiedTick, MetaDataComponent? metaData = null)
    {
        if (!Resolve(uid, ref metaData, false))
        {
            lastModifiedTick = null;
            return false;
        }

        lastModifiedTick = metaData.EntityLastModifiedTick;
        return true;
    }

    /// <summary>
    ///     Attempts to retrieve the paused status on an entity.
    /// </summary>
    /// <returns>Whether the pause status could be retrieved.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryPaused(EntityUid uid, [NotNullWhen(true)] out bool? paused, MetaDataComponent? metaData = null)
    {
        if (!Resolve(uid, ref metaData, false))
        {
            paused = null;
            return false;
        }

        paused = metaData.EntityPaused;
        return true;
    }

    /// <inheritdoc cref="IEntityManager.ToPrettyString"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityStringRepresentation ToPrettyString(EntityUid uid)
    {
        return EntityManager.ToPrettyString(uid);
    }

    /// <inheritdoc cref="IEntityManager.ToPrettyString"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityStringRepresentation ToPrettyString(NetEntity netEntity)
    {
        return EntityManager.ToPrettyString(netEntity);
    }

    #endregion

    #region Component Get

    /// <inheritdoc cref="IEntityManager.GetComponent&lt;T&gt;"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected T Comp<T>(EntityUid uid) where T : Component
    {
        return EntityManager.GetComponent<T>(uid);
    }

    /// <summary>
    ///     Returns the component of a specific type, or null when it's missing or the entity does not exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected T? CompOrNull<T>(EntityUid uid) where T : class, IComponent
    {
        return EntityManager.GetComponentOrNull<T>(uid);
    }

    /// <summary>
    ///     Returns the component of a specific type, or null when it's missing or the entity does not exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected T? CompOrNull<T>(EntityUid? uid) where T : class, IComponent
    {
        return uid.HasValue ? EntityManager.GetComponentOrNull<T>(uid.Value) : null;
    }

    /// <inheritdoc cref="IEntityManager.TryGetComponent&lt;T&gt;(EntityUid, out T)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryComp<T>(EntityUid uid, [NotNullWhen(true)] out T? comp)
    {
        return EntityManager.TryGetComponent(uid, out comp);
    }

    /// <inheritdoc cref="IEntityManager.TryGetComponent&lt;T&gt;(EntityUid?, out T)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TryComp<T>([NotNullWhen(true)] EntityUid? uid, [NotNullWhen(true)] out T? comp)
    {
        if (!uid.HasValue)
        {
            comp = default;
            return false;
        }

        return EntityManager.TryGetComponent(uid.Value, out comp);
    }

    /// <inheritdoc cref="IEntityManager.GetComponents"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected IEnumerable<IComponent> AllComps(EntityUid uid)
    {
        return EntityManager.GetComponents(uid);
    }

    /// <inheritdoc cref="IEntityManager.GetComponents&lt;T&gt;"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected IEnumerable<T> AllComps<T>(EntityUid uid)
    {
        return EntityManager.GetComponents<T>(uid);
    }

    /// <summary>
    ///     Returns the <see cref="TransformComponent"/> on an entity.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the entity doesn't exist.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected TransformComponent Transform(EntityUid uid)
    {
        return EntityManager.GetComponent<TransformComponent>(uid);
    }

    /// <summary>
    ///     Returns the <see cref="MetaDataComponent"/> on an entity.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown when the entity doesn't exist.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected MetaDataComponent MetaData(EntityUid uid)
    {
        return EntityManager.GetComponent<MetaDataComponent>(uid);
    }

    #endregion

    #region Component Has

    /// <summary>
    ///     Retrieves whether the entity has the specified component or not.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool HasComp<T>(EntityUid uid)
    {
        return EntityManager.HasComponent<T>(uid);
    }

    /// <summary>
    ///     Retrieves whether the entity has the specified component or not.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool HasComp(EntityUid uid, Type type)
    {
        return EntityManager.HasComponent(uid, type);
    }

    /// <summary>
    ///     Retrieves whether the entity has the specified component or not.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool HasComp<T>([NotNullWhen(true)] EntityUid? uid)
    {
        return EntityManager.HasComponent<T>(uid);
    }

    /// <summary>
    ///     Retrieves whether the entity has the specified component or not.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool HasComp([NotNullWhen(true)] EntityUid? uid, Type type)
    {
        return EntityManager.HasComponent(uid, type);
    }

    #endregion

    #region Component Add

    /// <inheritdoc cref="IEntityManager.AddComponent&lt;T&gt;(EntityUid)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected T AddComp<T>(EntityUid uid) where T :  Component, new()
    {
        return EntityManager.AddComponent<T>(uid);
    }

    /// <inheritdoc cref="IEntityManager.AddComponent&lt;T&gt;(EntityUid, T, bool)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void AddComp<T>(EntityUid uid, T component, bool overwrite = false) where T : Component
    {
        EntityManager.AddComponent(uid, component, overwrite);
    }

    /// <inheritdoc cref="IEntityManager.EnsureComponent&lt;T&gt;(EntityUid)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected T EnsureComp<T>(EntityUid uid) where T : Component, new()
    {
        return EntityManager.EnsureComponent<T>(uid);
    }

    #endregion

    #region Component Remove Deferred

    /// <inheritdoc cref="IEntityManager.RemoveComponentDeferred&lt;T&gt;(EntityUid)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool RemCompDeferred<T>(EntityUid uid) where T : class, IComponent
    {
        return EntityManager.RemoveComponentDeferred<T>(uid);
    }

    /// <inheritdoc cref="IEntityManager.RemoveComponentDeferred(EntityUid, Type)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool RemCompDeferred(EntityUid uid, Type type)
    {
        return EntityManager.RemoveComponentDeferred(uid, type);
    }

    /// <inheritdoc cref="IEntityManager.RemoveComponentDeferred(EntityUid, Component)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void RemCompDeferred(EntityUid uid, Component component)
    {
        EntityManager.RemoveComponentDeferred(uid, component);
    }

    /// <inheritdoc cref="IEntityManager.RemoveComponentDeferred(EntityUid, IComponent)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void RemCompDeferred(EntityUid uid, IComponent component)
    {
        EntityManager.RemoveComponentDeferred(uid, component);
    }
    #endregion

    #region Component count

    /// <inheritdoc cref="IEntityManager.Count" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected int Count<T>() where T : Component
    {
        return EntityManager.Count<T>();
    }

    /// <inheritdoc cref="IEntityManager.Count" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected int Count(Type type)
    {
        return EntityManager.Count(type);
    }

    #endregion

    #region Component Remove

    /// <inheritdoc cref="IEntityManager.RemoveComponent&lt;T&gt;(EntityUid)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool RemComp<T>(EntityUid uid) where T : class, IComponent
    {
        return EntityManager.RemoveComponent<T>(uid);
    }

    /// <inheritdoc cref="IEntityManager.RemoveComponent(EntityUid, Type)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool RemComp(EntityUid uid, Type type)
    {
        return EntityManager.RemoveComponent(uid, type);
    }

    /// <inheritdoc cref="IEntityManager.RemoveComponent(EntityUid, Component)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void RemComp(EntityUid uid, Component component)
    {
        EntityManager.RemoveComponent(uid, component);
    }

    /// <inheritdoc cref="IEntityManager.RemoveComponent(EntityUid, IComponent)"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void RemComp(EntityUid uid, IComponent component)
    {
        EntityManager.RemoveComponent(uid, component);
    }
    #endregion

    #region Entity Delete

    /// <inheritdoc cref="IEntityManager.DeleteEntity(EntityUid)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Del(EntityUid uid)
    {
        EntityManager.DeleteEntity(uid);
    }

    /// <inheritdoc cref="IEntityManager.QueueDeleteEntity(EntityUid)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void QueueDel(EntityUid uid)
    {
        EntityManager.QueueDeleteEntity(uid);
    }

    #endregion

    #region Entity Spawning

    // This method will be obsoleted soon.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityUid Spawn(string? prototype, EntityCoordinates coordinates)
    {
        return ((IEntityManager)EntityManager).SpawnEntity(prototype, coordinates);
    }

    /// <inheritdoc cref="IEntityManager.Spawn(string?, MapCoordinates, ComponentRegistry?)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityUid Spawn(string? prototype, MapCoordinates coordinates)
        => EntityManager.Spawn(prototype, coordinates);

    /// <inheritdoc cref="IEntityManager.Spawn(string?, ComponentRegistry?)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityUid Spawn(string? prototype)
        => EntityManager.Spawn(prototype);

    /// <inheritdoc cref="IEntityManager.SpawnAttachedTo" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityUid SpawnAttachedTo(string? prototype, EntityCoordinates coordinates)
        => EntityManager.SpawnAttachedTo(prototype, coordinates);

    /// <inheritdoc cref="IEntityManager.SpawnAtPosition" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityUid SpawnAtPosition(string? prototype, EntityCoordinates coordinates)
        => EntityManager.SpawnAtPosition(prototype, coordinates);

    /// <inheritdoc cref="IEntityManager.TrySpawnInContainer" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TrySpawnInContainer(
        string? protoName,
        EntityUid containerUid,
        string containerId,
        [NotNullWhen(true)] out EntityUid? uid,
        ContainerManagerComponent? containerComp = null,
        ComponentRegistry? overrides = null)
    {
        return EntityManager.TrySpawnInContainer(protoName, containerUid, containerId, out uid, containerComp, overrides);
    }

    /// <inheritdoc cref="IEntityManager.TrySpawnNextTo" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool TrySpawnNextTo(
        string? protoName,
        EntityUid target,
        [NotNullWhen(true)] out EntityUid? uid,
        TransformComponent? xform = null,
        ComponentRegistry? overrides = null)
    {
        return EntityManager.TrySpawnNextTo(protoName, target, out uid, xform, overrides);
    }

    /// <inheritdoc cref="IEntityManager.SpawnNextToOrDrop" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityUid SpawnNextToOrDrop(
        string? protoName,
        EntityUid target,
        TransformComponent? xform = null,
        ComponentRegistry? overrides = null)
    {
        return EntityManager.SpawnNextToOrDrop(protoName, target, xform, overrides);
    }

    /// <inheritdoc cref="IEntityManager.SpawnInContainerOrDrop" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityUid SpawnInContainerOrDrop(
        string? protoName,
        EntityUid containerUid,
        string containerId,
        TransformComponent? xform = null,
        ContainerManagerComponent? container = null,
        ComponentRegistry? overrides = null)
    {
        return EntityManager.SpawnInContainerOrDrop(protoName, containerUid, containerId, xform, container, overrides);
    }

    #endregion

    #region Utils

    /// <summary>
    ///     Utility static method to create an exception to be thrown when an entity doesn't have a specific component.
    /// </summary>
    private static KeyNotFoundException CompNotFound<T>(EntityUid uid)
        => new($"Entity {uid} does not have a component of type {typeof(T)}");

    #endregion

    #region All Entity Query

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected AllEntityQueryEnumerator<TComp1> AllEntityQuery<TComp1>() where TComp1 : Component
    {
        return EntityManager.AllEntityQueryEnumerator<TComp1>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected AllEntityQueryEnumerator<TComp1, TComp2> AllEntityQuery<TComp1, TComp2>()
        where TComp1 : Component
        where TComp2 : Component
    {
        return EntityManager.AllEntityQueryEnumerator<TComp1, TComp2>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected AllEntityQueryEnumerator<TComp1, TComp2, TComp3> AllEntityQuery<TComp1, TComp2, TComp3>()
        where TComp1 : Component
        where TComp2 : Component
        where TComp3 : Component
    {
        return EntityManager.AllEntityQueryEnumerator<TComp1, TComp2, TComp3>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected AllEntityQueryEnumerator<TComp1, TComp2, TComp3, TComp4> AllEntityQuery<TComp1, TComp2, TComp3, TComp4>()
        where TComp1 : Component
        where TComp2 : Component
        where TComp3 : Component
        where TComp4 : Component
    {
        return EntityManager.AllEntityQueryEnumerator<TComp1, TComp2, TComp3, TComp4>();
    }

    #endregion

    #region Get Entity Query

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityQueryEnumerator<TComp1> EntityQueryEnumerator<TComp1>() where TComp1 : Component
    {
        return EntityManager.EntityQueryEnumerator<TComp1>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityQueryEnumerator<TComp1, TComp2> EntityQueryEnumerator<TComp1, TComp2>()
        where TComp1 : Component
        where TComp2 : Component
    {
        return EntityManager.EntityQueryEnumerator<TComp1, TComp2>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityQueryEnumerator<TComp1, TComp2, TComp3> EntityQueryEnumerator<TComp1, TComp2, TComp3>()
        where TComp1 : Component
        where TComp2 : Component
        where TComp3 : Component
    {
        return EntityManager.EntityQueryEnumerator<TComp1, TComp2, TComp3>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityQueryEnumerator<TComp1, TComp2, TComp3, TComp4> EntityQueryEnumerator<TComp1, TComp2, TComp3, TComp4>()
        where TComp1 : Component
        where TComp2 : Component
        where TComp3 : Component
        where TComp4 : Component
    {
        return EntityManager.EntityQueryEnumerator<TComp1, TComp2, TComp3, TComp4>();
    }

    #endregion

    #region Entity Query

    /// <remarks>
    /// If you need the EntityUid, use <see cref="EntityQueryEnumerator{TComp1}"/>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure]
    protected EntityQuery<T> GetEntityQuery<T>() where T : Component
    {
        return EntityManager.GetEntityQuery<T>();
    }

    /// <remarks>
    /// If you need the EntityUid, use <see cref="EntityQueryEnumerator{TComp1}"/>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected IEnumerable<TComp1> EntityQuery<TComp1>(bool includePaused = false) where TComp1 : Component
    {
        return EntityManager.EntityQuery<TComp1>(includePaused);
    }

    /// <remarks>
    /// If you need the EntityUid, use <see cref="EntityQueryEnumerator{TComp1, TComp2}"/>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected IEnumerable<(TComp1, TComp2)> EntityQuery<TComp1, TComp2>(bool includePaused = false)
        where TComp1 : Component
        where TComp2 : Component
    {
        return EntityManager.EntityQuery<TComp1, TComp2>(includePaused);
    }

    /// <remarks>
    /// If you need the EntityUid, use <see cref="EntityQueryEnumerator{TComp1, TComp2, TComp3}"/>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected IEnumerable<(TComp1, TComp2, TComp3)> EntityQuery<TComp1, TComp2, TComp3>(bool includePaused = false)
        where TComp1 : Component
        where TComp2 : Component
        where TComp3 : Component
    {
        return EntityManager.EntityQuery<TComp1, TComp2, TComp3>(includePaused);
    }

    /// <remarks>
    /// If you need the EntityUid, use <see cref="EntityQueryEnumerator{TComp1, TComp2, TComp3, TComp4}"/>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected IEnumerable<(TComp1, TComp2, TComp3, TComp4)> EntityQuery<TComp1, TComp2, TComp3, TComp4>(bool includePaused = false)
        where TComp1 : Component
        where TComp2 : Component
        where TComp3 : Component
        where TComp4 : Component
    {
        return EntityManager.EntityQuery<TComp1, TComp2, TComp3, TComp4>(includePaused);
    }

    #endregion

    #region Networked Events

    /// <summary>
    ///     Sends a networked message to the server, while also repeatedly raising it locally for every time this tick gets re-predicted.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void RaisePredictiveEvent<T>(T msg) where T : EntityEventArgs
    {
        EntityManager.RaisePredictiveEvent(msg);
    }

    #endregion

    #region NetEntities

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected bool IsClientSide(EntityUid entity)
    {
        return EntityManager.IsClientSide(entity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetEntity(NetEntity nEntity, [NotNullWhen(true)] out EntityUid? entity)
    {
        return EntityManager.TryGetEntity(nEntity, out entity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetEntity(NetEntity? nEntity, [NotNullWhen(true)] out EntityUid? entity)
    {
        return EntityManager.TryGetEntity(nEntity, out entity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetNetEntity(EntityUid uid, [NotNullWhen(true)] out NetEntity? netEntity, MetaDataComponent? metadata = null)
    {
        return EntityManager.TryGetNetEntity(uid, out netEntity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetNetEntity(EntityUid? uid, out NetEntity netEntity, MetaDataComponent? metadata = null)
    {
        return EntityManager.TryGetNetEntity(uid, out netEntity);
    }

    /// <summary>
    ///     Returns the <see cref="NetEntity"/> of an entity. Returns <see cref="NetEntity.Invalid"/> if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected NetEntity GetNetEntity(EntityUid uid, MetaDataComponent? metadata = null)
    {
        return EntityManager.GetNetEntity(uid, metadata);
    }

    /// <summary>
    ///     Returns the <see cref="NetEntity"/> of an entity.  Logs an error if the entity does not exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected NetEntity? GetNetEntity(EntityUid? uid, MetaDataComponent? metadata = null)
    {
        return EntityManager.GetNetEntity(uid, metadata);
    }

    /// <summary>
    ///     Returns the <see cref="NetEntity"/> of an entity or creates a new entity if none exists.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityUid EnsureEntity<T>(NetEntity netEntity, EntityUid callerEntity)
    {
        return EntityManager.EnsureEntity<T>(netEntity, callerEntity);
    }

    /// <summary>
    ///     Returns the <see cref="NetEntity"/> of an entity or creates a new one if not null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityUid? EnsureEntity<T>(NetEntity? netEntity, EntityUid callerEntity)
    {
        return EntityManager.EnsureEntity<T>(netEntity, callerEntity);
    }

    /// <summary>
    ///     Returns the <see cref="NetCoordinates"/> of an entity or creates a new entity if none exists.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityCoordinates EnsureCoordinates<T>(NetCoordinates netCoordinates, EntityUid callerEntity)
    {
        return EntityManager.EnsureCoordinates<T>(netCoordinates, callerEntity);
    }

    /// <summary>
    ///     Returns the <see cref="NetCoordinates"/> of an entity or creates a new one if not null.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityCoordinates? EnsureCoordinates<T>(NetCoordinates? netCoordinates, EntityUid callerEntity)
    {
        return EntityManager.EnsureCoordinates<T>(netCoordinates, callerEntity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HashSet<EntityUid> EnsureEntitySet<T>(HashSet<NetEntity> netEntities, EntityUid callerEntity)
    {
        return EntityManager.EnsureEntitySet<T>(netEntities, callerEntity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<EntityUid> EnsureEntityList<T>(List<NetEntity> netEntities, EntityUid callerEntity)
    {
        return EntityManager.EnsureEntityList<T>(netEntities, callerEntity);
    }

    /// <summary>
    ///     Returns the <see cref="EntityUid"/> of a <see cref="NetEntity"/>. Returns <see cref="EntityUid.Invalid"/> if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityUid GetEntity(NetEntity netEntity)
    {
        return EntityManager.GetEntity(netEntity);
    }

    /// <summary>
    ///     Returns the <see cref="EntityUid"/> of a <see cref="NetEntity"/>. Returns <see cref="EntityUid.Invalid"/> if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityUid? GetEntity(NetEntity? netEntity)
    {
        return EntityManager.GetEntity(netEntity);
    }

    /// <summary>
    ///     Returns the <see cref="NetEntity"/> versions of the supplied entities. Logs an error if the entities do not exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected HashSet<NetEntity> GetNetEntitySet(HashSet<EntityUid> uids)
    {
        return EntityManager.GetNetEntitySet(uids);
    }

    /// <summary>
    ///     Returns the <see cref="EntityUid"/> versions of the supplied <see cref="NetEntity"/>. Returns <see cref="EntityUid.Invalid"/> if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected HashSet<EntityUid> GetEntitySet(HashSet<NetEntity> netEntities)
    {
        return EntityManager.GetEntitySet(netEntities);
    }

    /// <summary>
    ///     Returns the <see cref="NetEntity"/> versions of the supplied entities. Logs an error if the entities do not exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected List<NetEntity> GetNetEntityList(ICollection<EntityUid> uids)
    {
        return EntityManager.GetNetEntityList(uids);
    }

    /// <summary>
    ///     Returns the <see cref="NetEntity"/> versions of the supplied entities. Logs an error if the entities do not exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected List<NetEntity> GetNetEntityList(IReadOnlyList<EntityUid> uids)
    {
        return EntityManager.GetNetEntityList(uids);
    }

    /// <summary>
    ///     Returns the <see cref="EntityUid"/> versions of the supplied <see cref="NetEntity"/>. Returns <see cref="EntityUid.Invalid"/> if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected List<EntityUid> GetEntityList(ICollection<NetEntity> netEntities)
    {
        return EntityManager.GetEntityList(netEntities);
    }

    /// <summary>
    ///     Returns the <see cref="NetEntity"/> versions of the supplied entities. Logs an error if the entities do not exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected List<NetEntity> GetNetEntityList(List<EntityUid> uids)
    {
        return EntityManager.GetNetEntityList(uids);
    }

    /// <summary>
    ///     Returns the <see cref="EntityUid"/> versions of the supplied <see cref="NetEntity"/>. Returns <see cref="EntityUid.Invalid"/> if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected List<EntityUid> GetEntityList(List<NetEntity> netEntities)
    {
        return EntityManager.GetEntityList(netEntities);
    }

    /// <summary>
    ///     Returns the <see cref="NetEntity"/> versions of the supplied entities. Logs an error if the entities do not exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected List<NetEntity?> GetNetEntityList(List<EntityUid?> uids)
    {
        return EntityManager.GetNetEntityList(uids);
    }

    /// <summary>
    ///     Returns the <see cref="EntityUid"/> versions of the supplied <see cref="NetEntity"/>. Returns <see cref="EntityUid.Invalid"/> if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected List<EntityUid?> GetEntityList(List<NetEntity?> netEntities)
    {
        return EntityManager.GetEntityList(netEntities);
    }

    /// <summary>
    ///     Returns the <see cref="NetEntity"/> versions of the supplied entities. Logs an error if the entities do not exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected NetEntity[] GetNetEntityArray(EntityUid[] uids)
    {
        return EntityManager.GetNetEntityArray(uids);
    }

    /// <summary>
    ///     Returns the <see cref="EntityUid"/> versions of the supplied <see cref="NetEntity"/>. Returns <see cref="EntityUid.Invalid"/> if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityUid[] GetEntityArray(NetEntity[] netEntities)
    {
        return EntityManager.GetEntityArray(netEntities);
    }

    /// <summary>
    ///     Returns the <see cref="NetEntity"/> versions of the supplied entities.  Logs an error if the entities do not exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected NetEntity?[] GetNetEntityArray(EntityUid?[] uids)
    {
        return EntityManager.GetNetEntityArray(uids);
    }

    /// <summary>
    ///     Returns the <see cref="EntityUid"/> versions of the supplied <see cref="NetEntity"/>. Returns <see cref="EntityUid.Invalid"/> if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityUid?[] GetEntityArray(NetEntity?[] netEntities)
    {
        return EntityManager.GetEntityArray(netEntities);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected NetCoordinates GetNetCoordinates(EntityCoordinates coordinates, MetaDataComponent? metadata = null)
    {
        return EntityManager.GetNetCoordinates(coordinates, metadata);
    }

    /// <summary>
    ///     Returns the <see cref="NetEntity"/> of an entity. Returns <see cref="NetEntity.Invalid"/> if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected NetCoordinates? GetNetCoordinates(EntityCoordinates? coordinates, MetaDataComponent? metadata = null)
    {
        return EntityManager.GetNetCoordinates(coordinates, metadata);
    }

    /// <summary>
    ///     Returns the <see cref="EntityUid"/> of a <see cref="NetEntity"/>. Returns <see cref="EntityUid.Invalid"/> if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityCoordinates GetCoordinates(NetCoordinates netEntity)
    {
        return EntityManager.GetCoordinates(netEntity);
    }

    /// <summary>
    ///     Returns the <see cref="EntityUid"/> of a <see cref="NetEntity"/>. Returns <see cref="EntityUid.Invalid"/> if it doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected EntityCoordinates? GetCoordinates(NetCoordinates? netEntity)
    {
        return EntityManager.GetCoordinates(netEntity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HashSet<EntityCoordinates> GetEntitySet(HashSet<NetCoordinates> netEntities)
    {
        return EntityManager.GetEntitySet(netEntities);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<EntityCoordinates> GetEntityList(List<NetCoordinates> netEntities)
    {
        return EntityManager.GetEntityList(netEntities);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<EntityCoordinates> GetEntityList(ICollection<NetCoordinates> netEntities)
    {
        return EntityManager.GetEntityList(netEntities);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<EntityCoordinates?> GetEntityList(List<NetCoordinates?> netEntities)
    {
        return EntityManager.GetEntityList(netEntities);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityCoordinates[] GetEntityArray(NetCoordinates[] netEntities)
    {
        return EntityManager.GetEntityArray(netEntities);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public EntityCoordinates?[] GetEntityArray(NetCoordinates?[] netEntities)
    {
        return EntityManager.GetEntityArray(netEntities);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public HashSet<NetCoordinates> GetNetCoordinatesSet(HashSet<EntityCoordinates> entities)
    {
        return EntityManager.GetNetCoordinatesSet(entities);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<NetCoordinates> GetNetCoordinatesList(List<EntityCoordinates> entities)
    {
        return EntityManager.GetNetCoordinatesList(entities);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<NetCoordinates> GetNetCoordinatesList(ICollection<EntityCoordinates> entities)
    {
        return EntityManager.GetNetCoordinatesList(entities);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public List<NetCoordinates?> GetNetCoordinatesList(List<EntityCoordinates?> entities)
    {
        return EntityManager.GetNetCoordinatesList(entities);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NetCoordinates[] GetNetCoordinatesArray(EntityCoordinates[] entities)
    {
        return EntityManager.GetNetCoordinatesArray(entities);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NetCoordinates?[] GetNetCoordinatesArray(EntityCoordinates?[] entities)
    {
        return EntityManager.GetNetCoordinatesArray(entities);
    }

    #endregion
}

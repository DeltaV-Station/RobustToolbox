﻿using System.Diagnostics.CodeAnalysis;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Robust.Shared.RTShell;

public abstract partial class ConsoleCommand
{
    [Dependency] protected readonly IEntityManager EntityManager = default!;
    [Dependency] protected readonly IEntitySystemManager EntitySystemManager = default!;

    protected void Del(EntityUid entityUid)
        => EntityManager.DeleteEntity(entityUid);

    protected bool Deleted(EntityUid entity)
        => EntityManager.Deleted(entity);

    protected T Comp<T>(EntityUid entity)
        where T: IComponent
        => EntityManager.GetComponent<T>(entity);

    protected bool HasComp<T>(EntityUid entityUid)
        where T: IComponent
        => EntityManager.HasComponent<T>(entityUid);

    protected bool TryComp<T>(EntityUid? entity, [NotNullWhen(true)] out T? component)
        where T: IComponent
        => EntityManager.TryGetComponent<T>(entity, out component);

    protected bool TryComp<T>(EntityUid entity, [NotNullWhen(true)] out T? component)
        where T: IComponent
        => EntityManager.TryGetComponent<T>(entity, out component);

    protected T AddComp<T>(EntityUid entity)
        where T : Component, new()
        => EntityManager.AddComponent<T>(entity);

    protected T EnsureComp<T>(EntityUid entity)
        where T: Component, new()
        => EntityManager.EnsureComponent<T>(entity);

    protected T GetSys<T>()
        where T: EntitySystem
        => EntitySystemManager.GetEntitySystem<T>();

    protected EntityQuery<T> GetEntityQuery<T>()
        where T : Component
        => EntityManager.GetEntityQuery<T>();
}

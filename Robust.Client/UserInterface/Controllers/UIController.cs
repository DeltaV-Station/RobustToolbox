﻿using System.Diagnostics.CodeAnalysis;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Robust.Client.UserInterface.Controllers;

// Notices your UIController, *UwU Whats this?*
/// <summary>
///     Each <see cref="UIController"/> is instantiated as a singleton by <see cref="UserInterfaceManager"/>
///     <see cref="UIController"/> can use <see cref="DependencyAttribute"/> for regular IoC dependencies
///     and <see cref="UISystemDependencyAttribute"/> to depend on <see cref="EntitySystem"/>s, which will be automatically
///     injected once they are created.
/// </summary>
public abstract partial class UIController
{
    [Dependency] protected IUserInterfaceManager UIManager = default!;
    [Dependency] protected IEntitySystemManager EntitySystemManager = default!;
    [Dependency] protected IEntityManager EntityManager = default!;

    public virtual void Initialize()
    {
    }

    public virtual void FrameUpdate(FrameEventArgs args)
    {
    }
}

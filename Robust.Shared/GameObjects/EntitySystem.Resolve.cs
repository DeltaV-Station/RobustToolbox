using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Robust.Shared.Utility;

namespace Robust.Shared.GameObjects
{
    public abstract partial class EntitySystem
    {
        /// <summary>
        ///     Resolves the component on the entity but only if the component instance is null.
        /// </summary>
        /// <param name="uid">The entity where to query the components.</param>
        /// <param name="component">A reference to the variable storing the component, or null if it has to be resolved.</param>
        /// <typeparam name="TComp">The component type to resolve.</typeparam>
        /// <returns>True if the component is not null or was resolved correctly, false if the component couldn't be resolved.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool Resolve<TComp>(EntityUid uid, [NotNullWhen(true)] ref TComp? component)
            where TComp : IComponent
        {
            DebugTools.Assert(component == null || uid == component.Owner.Uid, "Specified Entity is not the component's Owner!");
            return component != null || ComponentManager.TryGetComponent(uid, out component);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="comp1"></param>
        /// <param name="comp2"></param>
        /// <typeparam name="TComp1"></typeparam>
        /// <typeparam name="TComp2"></typeparam>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool Resolve<TComp1, TComp2>(EntityUid uid, [NotNullWhen(true)] ref TComp1? comp1, [NotNullWhen(true)] ref TComp2? comp2)
            where TComp1 : IComponent
            where TComp2 : IComponent
        {
            return Resolve(uid, ref comp1) && Resolve(uid, ref comp2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool Resolve<TComp1, TComp2, TComp3>(EntityUid uid, [NotNullWhen(true)] ref TComp1? comp1, [NotNullWhen(true)] ref TComp2? comp2, [NotNullWhen(true)] ref TComp3? comp3)
            where TComp1 : IComponent
            where TComp2 : IComponent
            where TComp3 : IComponent
        {
            return Resolve(uid, ref comp1, ref comp2) && Resolve(uid, ref comp3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool Resolve<TComp1, TComp2, TComp3, TComp4>(EntityUid uid, [NotNullWhen(true)] ref TComp1? comp1, [NotNullWhen(true)] ref TComp2? comp2, [NotNullWhen(true)] ref TComp3? comp3, [NotNullWhen(true)] ref TComp4? comp4)
            where TComp1 : IComponent
            where TComp2 : IComponent
            where TComp3 : IComponent
            where TComp4 : IComponent
        {
            return Resolve(uid, ref comp1, ref comp2) && Resolve(uid, ref comp3, ref comp4);
        }
    }
}

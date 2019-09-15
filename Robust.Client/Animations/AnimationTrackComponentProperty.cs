using System;
using JetBrains.Annotations;
using Robust.Shared.Animations;
using Robust.Shared.Interfaces.GameObjects;

namespace Robust.Client.Animations
{
    [UsedImplicitly]
    public sealed class AnimationTrackComponentProperty : AnimationTrackProperty
    {
        public Type ComponentType { get; set; }
        public string Property { get; set; }

        protected override void ApplyProperty(object context, object value)
        {
            var entity = (IEntity) context;
            var component = entity.GetComponent(ComponentType);

            if (component is IAnimationProperties properties)
            {
                properties.SetAnimatableProperty(Property, value);
            }
            else
            {
                AnimationHelper.SetAnimatableProperty(component, Property, value);
            }
        }
    }
}

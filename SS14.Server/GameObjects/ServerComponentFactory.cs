﻿using SS14.Server.GameObjects.Components.Container;
using SS14.Server.Interfaces.GameObjects;
using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.GameObjects.Components;

namespace SS14.Server.GameObjects
{
    public class ServerComponentFactory : ComponentFactory
    {
        public ServerComponentFactory()
        {
            RegisterIgnore("Icon");

            Register<BasicActorComponent>();
            RegisterReference<BasicActorComponent, IActorComponent>();

            Register<CollidableComponent>();
            RegisterReference<CollidableComponent, ICollidableComponent>();
            Register<BoundingBoxComponent>();
            Register<KeyBindingInputComponent>();
            Register<PointLightComponent>();

            Register<PlayerInputMoverComponent>();
            RegisterReference<PlayerInputMoverComponent, IMoverComponent>();

            Register<ParticleSystemComponent>();
            Register<PhysicsComponent>();
            Register<SpriteComponent>();
            Register<AnimatedSpriteComponent>();
            Register<WearableAnimatedSpriteComponent>();
            Register<TransformComponent>();
            RegisterReference<TransformComponent, ITransformComponent>();

            Register<ClickableComponent>();
            RegisterReference<ClickableComponent, IClickableComponent>();

            Register<ContainerManagerComponent>();

            Register<DamageableComponent>();
            Register<DestructibleComponent>();

            Register<TemperatureComponent>();
        }
    }
}

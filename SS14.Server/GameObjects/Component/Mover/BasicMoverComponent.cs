﻿using SFML.System;
using SS14.Shared.GameObjects;
using SS14.Shared.IoC;

namespace SS14.Server.GameObjects
{
    [IoCTarget]
    internal class BasicMoverComponent : Component
    {
        public override string Name => "BasicMover";
        public BasicMoverComponent()
        {
            Family = ComponentFamily.Mover;
        }

        public override ComponentReplyMessage RecieveMessage(object sender, ComponentMessageType type,
                                                             params object[] list)
        {
            ComponentReplyMessage reply = base.RecieveMessage(sender, type, list);

            if (sender == this)
                return ComponentReplyMessage.Empty;

            switch (type)
            {
                case ComponentMessageType.PhysicsMove:
                    Translate((float) list[0], (float) list[1]);
                    break;
            }
            return reply;
        }

        public void Translate(float x, float y)
        {
            Owner.GetComponent<TransformComponent>(ComponentFamily.Transform).Position = new Vector2f(x, y);
        }

    }
}

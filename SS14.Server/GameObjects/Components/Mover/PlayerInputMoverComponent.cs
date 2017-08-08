﻿using Lidgren.Network;
using OpenTK;
using SFML.System;
using SS14.Server.Interfaces.GameObjects;
using SS14.Shared;
using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.GameObjects.Components;
using SS14.Shared.IoC;

namespace SS14.Server.GameObjects
{
    //Moves the entity based on input from a Clientside PlayerInputMoverComponent.
    public class PlayerInputMoverComponent : Component, IMoverComponent
    {
        public override string Name => "PlayerInputMover";
        public override uint? NetID => NetIDs.PLAYER_INPUT_MOVER;
        public override bool NetworkSynchronizeExistence => true;

        /// <summary>
        /// Handles position messages. that should be it.
        /// </summary>
        /// <param name="message"></param>
        public override void HandleNetworkMessage(IncomingEntityComponentMessage message, NetConnection client)
        {
            var physComp = Owner.GetComponent<PhysicsComponent>();
            var transform = Owner.GetComponent<ITransformComponent>();

            physComp.Velocity = new Vector2((float)message.MessageParameters[2], (float)message.MessageParameters[3]);
            transform.Position = new Vector2f((float)message.MessageParameters[0], (float)message.MessageParameters[1]);
        }
    }
}

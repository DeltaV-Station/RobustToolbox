﻿using SS14.Client.Interfaces.GameObjects;

namespace SS14.Client.GameObjects
{
    //Moves an entity based on key binding input
    public class PlayerInputMoverComponent : ClientComponent, IMoverComponent
    {
        /// <inheritdoc />
        public override string Name => "PlayerInputMover";

        /// <inheritdoc />
        public override uint? NetID => null;

        /// <inheritdoc />
        public override bool NetworkSynchronizeExistence => false;

        // This does nothing on the client.
    }
}

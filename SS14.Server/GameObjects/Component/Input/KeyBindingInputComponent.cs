﻿using Lidgren.Network;
using SS14.Server.GameObjects.Events;
using SS14.Shared;
using SS14.Shared.GameObjects;
using SS14.Shared.IoC;
using System.Collections.Generic;

namespace SS14.Server.GameObjects
{
    /// <summary>
    /// This class recieves keypresses from the attached client and forwards them to other components.
    /// </summary>
    [IoCTarget]
    public class KeyBindingInputComponent : Component
    {
        public override string Name => "KeyBindingInput";
        public override void HandleNetworkMessage(IncomingEntityComponentMessage message, NetConnection client)
        {
            var keyFunction = (BoundKeyFunctions) message.MessageParameters[0];
            var keyState = (BoundKeyState) message.MessageParameters[1];

            Owner.SendMessage(this, ComponentMessageType.BoundKeyChange, keyFunction, keyState);
            Owner.RaiseEvent(new BoundKeyChangeEventArgs{KeyFunction = keyFunction, KeyState = keyState, Actor = Owner});
            var boolState = keyState == BoundKeyState.Down;

            SetKeyState(keyFunction, boolState);
        }

        private readonly Dictionary<BoundKeyFunctions, bool> _keyStates = new Dictionary<BoundKeyFunctions, bool>();

        public KeyBindingInputComponent()
        {
            Family = ComponentFamily.Input;
            _keyStates = new Dictionary<BoundKeyFunctions, bool>();
        }

        protected void SetKeyState(BoundKeyFunctions k, bool state)
        {
            // Check to see if we have a keyhandler for the key that's been pressed. Discard invalid keys.
            _keyStates[k] = state;
        }

        public bool GetKeyState(BoundKeyFunctions k)
        {
            if (_keyStates.ContainsKey(k))
                return _keyStates[k];
            return false;
        }
    }
}

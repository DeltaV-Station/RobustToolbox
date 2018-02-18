﻿using System.Collections.Generic;
using System.Linq;
using SS14.Client.Input;
using SS14.Client.Interfaces.Input;
using SS14.Shared.GameObjects;
using SS14.Shared.Input;
using SS14.Shared.IoC;

namespace SS14.Client.GameObjects
{
    public class KeyBindingInputComponent : Component
    {
        public override string Name => "KeyBindingInput";
        public override uint? NetID => NetIDs.KEY_BINDING_INPUT;

        #region Delegates

        public delegate void KeyEvent(bool state);

        #endregion Delegates

        private readonly Dictionary<BoundKeyFunctions, KeyEvent> _keyHandlers;
        private readonly Dictionary<BoundKeyFunctions, bool> _keyStates;

        private bool _enabled = true;

        public KeyBindingInputComponent()
        {
            _keyStates = new Dictionary<BoundKeyFunctions, bool>();
            _keyHandlers = new Dictionary<BoundKeyFunctions, KeyEvent>();
            //Set up keystates
        }

        public override void OnAdd()
        {
            base.OnAdd();

            var keyBindingManager = IoCManager.Resolve<IKeyBindingManager>();
            keyBindingManager.BoundKeyDown += KeyDown;
            keyBindingManager.BoundKeyUp += KeyUp;
        }

        public override void Shutdown()
        {
            base.Shutdown();
            var keyBindingManager = IoCManager.Resolve<IKeyBindingManager>();
            keyBindingManager.BoundKeyDown -= KeyDown;
            keyBindingManager.BoundKeyUp -= KeyUp;
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            if (_enabled)
                UpdateKeys(frameTime);
        }

        private void Enable()
        {
            _enabled = true;
        }

        private void Disable()
        {
            _enabled = false;

            //Remove all active key states and send keyup messages for them.
            foreach (var state in _keyStates.ToList())
            {
                var message = new BoundKeyChangedMsg(state.Key, BoundKeyState.Up);
                SendMessage(message);
                SendNetworkMessage(message);
                _keyStates.Remove(state.Key);
            }
        }

        public virtual void KeyDown(object sender, BoundKeyEventArgs e)
        {
            if (!_enabled || GetKeyState(e.Function))
                return; //Don't repeat keys that are already down.

            SetKeyState(e.Function, true);
            var message = new BoundKeyChangedMsg(e.Function, e.FunctionState);
            SendMessage(message);
            SendNetworkMessage(message);
        }

        public virtual void KeyUp(object sender, BoundKeyEventArgs e)
        {
            if (!_enabled)
                return;

            SetKeyState(e.Function, false);
            var message = new BoundKeyChangedMsg(e.Function, e.FunctionState);
            SendMessage(message);
            SendNetworkMessage(message);
        }

        protected void SetKeyState(BoundKeyFunctions k, bool state)
        {
            // Check to see if we have a keyhandler for the key that's been pressed. Discard invalid keys.
            _keyStates[k] = state;
        }

        public bool GetKeyState(BoundKeyFunctions k)
        {
            if (_keyStates.Keys.Contains(k))
                return _keyStates[k];
            return false;
        }

        public virtual void UpdateKeys(float frameTime)
        {
            // So basically we check for active keys with handlers and execute them. This is a linq query.
            // Get all of the active keys' handlers
            var activeKeyHandlers =
                from keyState in _keyStates
                join handler in _keyHandlers on keyState.Key equals handler.Key
                select new {evt = handler.Value, state = keyState.Value};

            //Execute the bastards!
            foreach (var keyHandler in activeKeyHandlers)
            {
                //If there's even one active, we set updateRequired so that this gets hit again next update
                //updateRequired = true; // QUICKNDIRTY
                var k = keyHandler.evt;
                k(keyHandler.state);
            }

            //Delete false states from the dictionary so they don't get reprocessed and fuck up other stuff.
            foreach (var state in _keyStates.ToList())
            {
                if (!state.Value)
                    _keyStates.Remove(state.Key);
                else
                    SendMessage(new BoundKeyRepeatMsg(state.Key, BoundKeyState.Repeat));
            }
        }
    }
}

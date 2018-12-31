﻿using SS14.Client.GodotGlue;
using System;

namespace SS14.Client.UserInterface.Controls
{
    [ControlWrap(typeof(Godot.BaseButton))]
    public abstract class BaseButton : Control
    {
        public BaseButton() : base()
        {
        }
        public BaseButton(string name) : base(name)
        {
        }
        internal BaseButton(Godot.BaseButton button) : base(button)
        {
        }

        public ActionMode Mode
        {
            get => (ActionMode)SceneControl.Get("action_mode");
            set => SceneControl.Set("action_mode", (Godot.BaseButton.ActionModeEnum)value);
        }

        public bool Disabled
        {
            get => (bool)SceneControl.Get("disabled");
            set => SceneControl.Set("disabled", value);
        }

        public bool Pressed
        {
            get => (bool)SceneControl.Get("pressed");
            set => SceneControl.Set("pressed", value);
        }

        public bool ToggleMode
        {
            get => (bool)SceneControl.Get("toggle_mode");
            set => SceneControl.Set("toggle_mode", value);
        }

        public enum ActionMode
        {
            Press = Godot.BaseButton.ActionModeEnum.Press,
            Release = Godot.BaseButton.ActionModeEnum.Release,
        }

        public bool IsHovered => (bool)SceneControl.Call("is_hovered");
        public DrawModeEnum DrawMode => (DrawModeEnum)SceneControl.Call("get_draw_mode");

        public ButtonGroup ButtonGroup
        {
            get => new ButtonGroup((Godot.ButtonGroup)SceneControl.Call("get_button_group"));
            set => SceneControl.Call("set_button_group", value?.GodotGroup);
        }

        public event Action<ButtonEventArgs> OnButtonDown;
        public event Action<ButtonEventArgs> OnButtonUp;
        public event Action<ButtonEventArgs> OnPressed;
        public event Action<ButtonToggledEventArgs> OnToggled;

        public enum DrawModeEnum
        {
            Normal = Godot.BaseButton.DrawMode.Normal,
            Pressed = Godot.BaseButton.DrawMode.Pressed,
            Hover = Godot.BaseButton.DrawMode.Hover,
            Disabled = Godot.BaseButton.DrawMode.Disabled,
        }

        public class ButtonEventArgs : EventArgs
        {
            /// <summary>
            ///     The button this event originated from.
            /// </summary>
            public BaseButton Button { get; }

            public ButtonEventArgs(BaseButton button)
            {
                Button = button;
            }
        }

        public class ButtonToggledEventArgs : ButtonEventArgs
        {
            /// <summary>
            ///     The new pressed state of the button.
            /// </summary>
            public bool Pressed { get; }

            public ButtonToggledEventArgs(bool pressed, BaseButton button) : base(button)
            {
                Pressed = pressed;
            }
        }

        private GodotSignalSubscriber0 __buttonDownSubscriber;
        private GodotSignalSubscriber0 __buttonUpSubscriber;
        private GodotSignalSubscriber1 __toggledSubscriber;
        private GodotSignalSubscriber0 __pressedSubscriber;

        protected override void SetupSignalHooks()
        {
            base.SetupSignalHooks();

            __buttonDownSubscriber = new GodotSignalSubscriber0();
            __buttonDownSubscriber.Connect(SceneControl, "button_down");
            __buttonDownSubscriber.Signal += __buttonDownHook;

            __buttonUpSubscriber = new GodotSignalSubscriber0();
            __buttonUpSubscriber.Connect(SceneControl, "button_up");
            __buttonUpSubscriber.Signal += __buttonUpHook;

            __toggledSubscriber = new GodotSignalSubscriber1();
            __toggledSubscriber.Connect(SceneControl, "toggled");
            __toggledSubscriber.Signal += __toggledHook;

            __pressedSubscriber = new GodotSignalSubscriber0();
            __pressedSubscriber.Connect(SceneControl, "pressed");
            __pressedSubscriber.Signal += __pressedHook;
        }

        protected override void DisposeSignalHooks()
        {
            base.DisposeSignalHooks();

            if (__buttonDownSubscriber != null)
            {
                __buttonDownSubscriber.Disconnect(SceneControl, "button_down");
                __buttonDownSubscriber.Dispose();
                __buttonDownSubscriber = null;
            }

            if (__buttonUpSubscriber != null)
            {
                __buttonUpSubscriber.Disconnect(SceneControl, "button_up");
                __buttonUpSubscriber.Dispose();
                __buttonUpSubscriber = null;
            }

            if (__toggledSubscriber != null)
            {
                __toggledSubscriber.Disconnect(SceneControl, "toggled");
                __toggledSubscriber.Dispose();
                __toggledSubscriber = null;
            }

            if (__pressedSubscriber != null)
            {
                __pressedSubscriber.Disconnect(SceneControl, "pressed");
                __pressedSubscriber.Dispose();
                __pressedSubscriber = null;
            }
        }

        private void __buttonDownHook()
        {
            OnButtonDown?.Invoke(new ButtonEventArgs(this));
        }

        private void __buttonUpHook()
        {
            OnButtonUp?.Invoke(new ButtonEventArgs(this));
        }

        private void __pressedHook()
        {
            OnPressed?.Invoke(new ButtonEventArgs(this));
        }

        private void __toggledHook(object state)
        {
            OnToggled?.Invoke(new ButtonToggledEventArgs((bool)state, this));
        }
    }

    public class ButtonGroup
    {
        public Godot.ButtonGroup GodotGroup { get; }

        public ButtonGroup() : this(new Godot.ButtonGroup()) { }
        public ButtonGroup(Godot.ButtonGroup group)
        {
            GodotGroup = group;
        }
    }
}

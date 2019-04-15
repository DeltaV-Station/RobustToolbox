﻿using Robust.Client.Input;
using Robust.Client.Utility;
using Robust.Shared.Maths;
using System;

namespace Robust.Client.UserInterface
{
    public partial class Control
    {
        protected internal virtual void MouseEntered()
        {
        }

        protected internal virtual void MouseExited()
        {
        }

        protected internal virtual void MouseWheel(GUIMouseWheelEventArgs args)
        {
        }

        public event Action<GUIMouseButtonEventArgs> OnMouseDown;

        protected internal virtual void MouseDown(GUIMouseButtonEventArgs args)
        {
            OnMouseDown?.Invoke(args);
        }

        protected internal virtual void MouseUp(GUIMouseButtonEventArgs args)
        {
        }

        protected internal virtual void MouseMove(GUIMouseMoveEventArgs args)
        {
        }

        public event Action<GUIKeyEventArgs> OnKeyDown;

        protected internal virtual void KeyDown(GUIKeyEventArgs args)
        {
            OnKeyDown?.Invoke(args);
        }

        protected internal virtual void KeyUp(GUIKeyEventArgs args)
        {
        }

        protected internal virtual void KeyHeld(GUIKeyEventArgs args)
        {
        }

        protected internal virtual void TextEntered(GUITextEventArgs args)
        {
        }

        private void HandleGuiInput(Godot.InputEvent input)
        {
            switch (input)
            {
                case Godot.InputEventKey keyEvent:
                    var keyEventArgs = new GUIKeyEventArgs(this,
                        Keyboard.ConvertGodotKey(keyEvent.Scancode),
                        keyEvent.Echo,
                        keyEvent.Alt,
                        keyEvent.Control,
                        keyEvent.Shift,
                        keyEvent.Command);
                    if (keyEvent.Pressed)
                    {
                        KeyDown(keyEventArgs);
                    }
                    else
                    {
                        KeyUp(keyEventArgs);
                    }

                    break;

                case Godot.InputEventMouseButton buttonEvent:
                    if (buttonEvent.ButtonIndex >= (int) Godot.ButtonList.WheelUp &&
                        buttonEvent.ButtonIndex <= (int) Godot.ButtonList.WheelRight)
                    {
                        // Mouse wheel event.
                        var mouseWheelEventArgs = new GUIMouseWheelEventArgs((Mouse.Wheel) buttonEvent.ButtonIndex,
                            this,
                            (Mouse.ButtonMask) buttonEvent.ButtonMask,
                            buttonEvent.GlobalPosition.Convert(),
                            buttonEvent.Position.Convert(),
                            buttonEvent.Alt,
                            buttonEvent.Control,
                            buttonEvent.Shift,
                            buttonEvent.Command);
                        MouseWheel(mouseWheelEventArgs);
                    }
                    else
                    {
                        // Mouse button event.
                        var mouseButtonEventArgs = new GUIMouseButtonEventArgs((Mouse.Button) buttonEvent.ButtonIndex,
                            buttonEvent.Doubleclick,
                            this,
                            (Mouse.ButtonMask) buttonEvent.ButtonMask,
                            buttonEvent.GlobalPosition.Convert(),
                            buttonEvent.Position.Convert(),
                            buttonEvent.Alt,
                            buttonEvent.Control,
                            buttonEvent.Shift,
                            buttonEvent.Command);
                        if (buttonEvent.Pressed)
                        {
                            MouseDown(mouseButtonEventArgs);
                        }
                        else
                        {
                            MouseUp(mouseButtonEventArgs);
                        }
                    }

                    break;

                case Godot.InputEventMouseMotion motionEvent:
                    var mouseMoveEventArgs = new GUIMouseMoveEventArgs(motionEvent.Relative.Convert(),
                        motionEvent.Speed.Convert(),
                        this,
                        (Mouse.ButtonMask) motionEvent.ButtonMask,
                        motionEvent.GlobalPosition.Convert(),
                        motionEvent.Position.Convert(),
                        motionEvent.Alt,
                        motionEvent.Control,
                        motionEvent.Shift,
                        motionEvent.Command);
                    MouseMove(mouseMoveEventArgs);
                    break;
            }
        }
    }

    public class GUIKeyEventArgs : KeyEventArgs
    {
        /// <summary>
        ///     The control spawning this event.
        /// </summary>
        public Control SourceControl { get; }

        public GUIKeyEventArgs(Control sourceControl,
            Keyboard.Key key,
            bool repeat,
            bool alt,
            bool control,
            bool shift,
            bool system)
            : base(key, repeat, alt, control, shift, system)
        {
            SourceControl = sourceControl;
        }
    }

    public class GUITextEventArgs : TextEventArgs
    {
        /// <summary>
        ///     The control spawning this event.
        /// </summary>
        public Control SourceControl { get; }

        public GUITextEventArgs(Control sourceControl,
            uint codePoint)
            : base(codePoint)
        {
            SourceControl = sourceControl;
        }
    }

    public abstract class GUIMouseEventArgs : ModifierInputEventArgs
    {
        /// <summary>
        ///     The control spawning this event.
        /// </summary>
        public Control SourceControl { get; internal set; }

        /// <summary>
        ///     <c>InputEventMouse.button_mask</c> in Godot.
        ///     Which mouse buttons are currently held maybe?
        /// </summary>
        public Mouse.ButtonMask ButtonMask { get; }

        /// <summary>
        ///     Position of the mouse, relative to the screen.
        /// </summary>
        public Vector2 GlobalPosition { get; }

        /// <summary>
        ///     Position of the mouse, relative to the current control.
        /// </summary>
        public Vector2 RelativePosition { get; internal set; }

        protected GUIMouseEventArgs(Control sourceControl,
            Mouse.ButtonMask buttonMask,
            Vector2 globalPosition,
            Vector2 relativePosition,
            bool alt,
            bool control,
            bool shift,
            bool system)
            : base(alt, control, shift, system)
        {
            SourceControl = sourceControl;
            ButtonMask = buttonMask;
            GlobalPosition = globalPosition;
            RelativePosition = relativePosition;
        }
    }

    public class GUIMouseButtonEventArgs : GUIMouseEventArgs
    {
        /// <summary>
        ///     The mouse button that has been pressed or released.
        /// </summary>
        public Mouse.Button Button { get; }

        /// <summary>
        ///     True if this action was a double click.
        ///     Can't be true if this was a release event.
        /// </summary>
        public bool DoubleClick { get; }

        public GUIMouseButtonEventArgs(Mouse.Button button,
            bool doubleClick,
            Control sourceControl,
            Mouse.ButtonMask buttonMask,
            Vector2 globalPosition,
            Vector2 relativePosition,
            bool alt,
            bool control,
            bool shift,
            bool system)
            : base(sourceControl, buttonMask, globalPosition, relativePosition, alt, control, shift, system)
        {
            Button = button;
            DoubleClick = doubleClick;
        }
    }

    public class GUIMouseMoveEventArgs : GUIMouseEventArgs
    {
        /// <summary>
        ///     The new position relative to the previous position.
        /// </summary>
        public Vector2 Relative { get; }

        // TODO: Godot's docs aren't exactly clear on what this is.
        //         Speed how?
        /// <summary>
        ///     The speed of the movement.
        /// </summary>
        public Vector2 Speed { get; }

        // ALL the parameters!
        public GUIMouseMoveEventArgs(Vector2 relative,
            Vector2 speed,
            Control sourceControl,
            Mouse.ButtonMask buttonMask,
            Vector2 globalPosition,
            Vector2 relativePosition,
            bool alt,
            bool control,
            bool shift,
            bool system)
            : base(sourceControl, buttonMask, globalPosition, relativePosition, alt, control, shift, system)
        {
            Relative = relative;
            Speed = speed;
        }
    }

    public class GUIMouseWheelEventArgs : GUIMouseEventArgs
    {
        /// <summary>
        ///     The direction the mouse wheel was moved in.
        /// </summary>
        public Mouse.Wheel WheelDirection { get; }

        public GUIMouseWheelEventArgs(Mouse.Wheel wheelDirection,
            Control sourceControl,
            Mouse.ButtonMask buttonMask,
            Shared.Maths.Vector2 globalPosition,
            Shared.Maths.Vector2 relativePosition,
            bool alt,
            bool control,
            bool shift,
            bool system)
            : base(sourceControl, buttonMask, globalPosition, relativePosition, alt, control, shift, system)
        {
            WheelDirection = wheelDirection;
        }
    }
}

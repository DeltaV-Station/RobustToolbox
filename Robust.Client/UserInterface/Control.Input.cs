﻿using Robust.Client.Input;
using Robust.Client.Utility;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using System;

namespace Robust.Client.UserInterface
{
    public partial class Control
    {
        /// <summary>
        ///     Invoked when the mouse enters the area of this control / when it hovers over the control.
        /// </summary>
        public event Action<GUIMouseHoverEventArgs> OnMouseEntered;

        protected internal virtual void MouseEntered()
        {
            OnMouseEntered?.Invoke(new GUIMouseHoverEventArgs(this));
        }

        /// <summary>
        ///     Invoked when the mouse exits the area of this control / when it stops hovering over the control.
        /// </summary>
        public event Action<GUIMouseHoverEventArgs> OnMouseExited;

        protected internal virtual void MouseExited()
        {
            OnMouseExited?.Invoke(new GUIMouseHoverEventArgs(this));
        }

        protected internal virtual void MouseWheel(GUIMouseWheelEventArgs args)
        {
        }

        public event Action<GUIBoundKeyEventArgs> OnKeyBindDown;

        protected internal virtual void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            OnKeyBindDown?.Invoke(args);
        }

        protected internal virtual void KeyBindUp(GUIBoundKeyEventArgs args)
        {
        }

        protected internal virtual void MouseMove(GUIMouseMoveEventArgs args)
        {
        }

        protected internal virtual void KeyHeld(GUIKeyEventArgs args)
        {
        }

        protected internal virtual void TextEntered(GUITextEventArgs args)
        {
        }
    }

    public class GUIMouseHoverEventArgs : EventArgs
    {
        /// <summary>
        ///     The control this event originated from.
        /// </summary>
        public Control SourceControl { get; }

        public GUIMouseHoverEventArgs(Control sourceControl)
        {
            SourceControl = sourceControl;
        }
    }

    public class GUIBoundKeyEventArgs : BoundKeyEventArgs
    {
        /// <summary>
        ///     Position of the mouse, relative to the current control.
        /// </summary>
        public Vector2 RelativePosition { get; internal set; }

        public Vector2 RelativePixelPosition { get; internal set; }

        public GUIBoundKeyEventArgs(BoundKeyFunction function, BoundKeyState state, ScreenCoordinates pointerLocation,
            bool canFocus, Vector2 relativePosition, Vector2 relativePixelPosition)
            : base(function, state, pointerLocation, canFocus)
        {
            RelativePosition = relativePosition;
            RelativePixelPosition = relativePixelPosition;
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

        public Vector2 GlobalPixelPosition { get; }

        /// <summary>
        ///     Position of the mouse, relative to the current control.
        /// </summary>
        public Vector2 RelativePosition { get; internal set; }

        public Vector2 RelativePixelPosition { get; internal set; }

        protected GUIMouseEventArgs(Control sourceControl,
            Mouse.ButtonMask buttonMask,
            Vector2 globalPosition,
            Vector2 globalPixelPosition,
            Vector2 relativePosition,
            Vector2 relativePixelPosition,
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
            RelativePixelPosition = relativePixelPosition;
            GlobalPixelPosition = globalPixelPosition;
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
            Vector2 globalPixelPosition,
            Vector2 relativePosition,
            Vector2 relativePixelPosition,
            bool alt,
            bool control,
            bool shift,
            bool system)
            : base(sourceControl, buttonMask, globalPosition, globalPixelPosition, relativePosition, relativePixelPosition, alt, control, shift, system)
        {
            Relative = relative;
            Speed = speed;
        }
    }

    public class GUIMouseWheelEventArgs : GUIMouseEventArgs
    {
        public Vector2 Delta { get; }

        public GUIMouseWheelEventArgs(Vector2 delta,
            Control sourceControl,
            Mouse.ButtonMask buttonMask,
            Vector2 globalPosition,
            Vector2 globalPixelPosition,
            Vector2 relativePosition,
            Vector2 relativePixelPosition,
            bool alt,
            bool control,
            bool shift,
            bool system)
            : base(sourceControl, buttonMask, globalPosition, globalPixelPosition, relativePosition, relativePixelPosition, alt, control, shift, system)
        {
            Delta = delta;
        }
    }
}

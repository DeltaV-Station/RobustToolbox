﻿using System;
using System.Collections.Generic;
using OpenTK.Graphics;
using SS14.Client.Graphics;
using SS14.Client.Graphics.Input;
using SS14.Client.Graphics.Render;
using SS14.Client.UserInterface.Components;
using SS14.Shared.Maths;

namespace SS14.Client.UserInterface.Controls
{
    public class ScrollableContainer : Control
    {
        [Obsolete("Parent the control to the Container instead.")]
        public readonly List<Control> Components = new List<Control>();

        protected readonly Scrollbar ScrollbarH;
        protected readonly Scrollbar ScrollbarV;

        protected bool Disposing;

        private RenderImage _clippingRi;
        private Control _innerFocus;
        private float _maxX;
        private float _maxY;

        public UiAnchor Container { get; }

        /// <summary>
        /// A panel thats displays a subsection of a larger internal screen.
        /// </summary>
        /// <param name="size">Size of the internal screen.</param>
        public ScrollableContainer(Vector2i size)
        {
            // the controls stars out with the same size as container
            Size = size;

            BackgroundColor = new Color4(169, 169, 169, 255);
            DrawBackground = true;
            DrawBorder = true;

            _clippingRi = new RenderImage("UI_SCR_CONTAINER", (uint) size.X, (uint) size.Y);
            _clippingRi.BlendSettings.ColorSrcFactor = BlendMode.Factor.SrcAlpha;
            _clippingRi.BlendSettings.ColorDstFactor = BlendMode.Factor.OneMinusSrcAlpha;
            _clippingRi.BlendSettings.AlphaSrcFactor = BlendMode.Factor.SrcAlpha;
            _clippingRi.BlendSettings.AlphaDstFactor = BlendMode.Factor.OneMinusSrcAlpha;

            ScrollbarH = new Scrollbar(true);
            ScrollbarV = new Scrollbar(false);
            
            Container = new UiAnchor();
            // Container.Size = size; // using this as a list
            Container.Position = Vector2i.Zero; //this must always be 0 to work with RT
            //Container.BackgroundColor = Color4.Magenta;
            Container.DrawBackground = false;
            Container.DrawBorder = false;
            // AddControl(Container); // this needs to always be at screenPos {0,0}, setting a parent breaks that

            ScrollbarH.Update(0);
            ScrollbarV.Update(0);
        }

        /// <inheritdoc />
        protected override void OnCalcRect()
        {
            // ugh...
            ScrollbarH.BarLength = ScrollbarV.Visible ? Size.X - ScrollbarV.ClientArea.Width : Size.X;
            ScrollbarV.BarLength = ScrollbarH.Visible ? Size.Y - ScrollbarH.ClientArea.Height : Size.Y;
            ScrollbarH.BarLength = ScrollbarV.Visible ? Size.X - ScrollbarV.ClientArea.Width : Size.X;

            ClientArea = Box2i.FromDimensions(new Vector2i(), Size);
        }

        protected override void OnCalcPosition()
        {
            ScrollbarH.Position = Position + new Vector2i(ClientArea.Left, ClientArea.Bottom - ScrollbarH.ClientArea.Height);
            ScrollbarV.Position = Position + new Vector2i(ClientArea.Right - ScrollbarV.ClientArea.Width, ClientArea.Top);

            base.OnCalcPosition();


            /*
            _maxX = 0;
            _maxY = 0;

            foreach (var component in Components)
            {
                if (component.Position.X + component.ClientArea.Width > _maxX)
                    _maxX = component.Position.X + component.ClientArea.Width;

                if (component.Position.Y + component.ClientArea.Height > _maxY)
                    _maxY = component.Position.Y + component.ClientArea.Height;
            }
            */
        }

        public override void DoLayout()
        {
            Container.DoLayout();

            /*
            foreach (var component in Components)
            {
                component.DoLayout();
                component.Position = new Vector2i(component.Position.X - (int) ScrollbarH.Value, component.Position.Y - (int) ScrollbarV.Value);
                component.Update(0);
            }
            */
            base.DoLayout();
        }

        public override void Update(float frameTime)
        {
            if (Disposing || !Visible) return;

            Container.Update(frameTime);
            /*
            if (_innerFocus != null && !Components.Contains(_innerFocus)) ClearFocus();
            */
            var bounds = Container.GetShrinkBounds(false);
            
            bounds = new Box2i(Container.Position, bounds.BottomRight); // screen to local size
            _maxX = bounds.Width;
            _maxY = bounds.Height;

            ScrollbarH.Max = (int) _maxX - ClientArea.Width + (_maxY > _clippingRi.Height ? ScrollbarV.ClientArea.Width : 0);
            ScrollbarH.Visible = _maxX > _clippingRi.Width;

            ScrollbarV.Max = (int) _maxY - ClientArea.Height + (_maxX > _clippingRi.Height ? ScrollbarH.ClientArea.Height : 0);
            ScrollbarV.Visible = _maxY > _clippingRi.Height;

            ScrollbarH.Update(frameTime);
            ScrollbarV.Update(frameTime);

            var xOff = ScrollbarH.Visible ? ScrollbarH.Value * -1 : 0;
            var yOff = ScrollbarV.Visible ? ScrollbarV.Value * -1 : 0;

            Container.ScrollOffset = new Vector2i((int) xOff, (int) yOff);
        }

        protected override void DrawContents()
        {
            base.DrawContents();

            // the rectangle should always be completely covered with draws, no point clearing
            _clippingRi.Clear(DrawBackground ? BackgroundColor : Color4.Transparent);
            
            _clippingRi.BeginDrawing();
            // draw the inner container screen
            {
                Container.Draw();
            }
            _clippingRi.EndDrawing();
            _clippingRi.Blit(Position.X + ClientArea.Left, Position.Y + ClientArea.Top, (uint)ClientArea.Height, (uint)ClientArea.Width, Color.White, BlitterSizeMode.None);
        }

        public override void Draw()
        {
            if (Disposing || !Visible) return;
            
            base.Draw();

            ScrollbarH.Draw();
            ScrollbarV.Draw();
        }

        public override void Dispose()
        {
            if (Disposing) return;
            Disposing = true;
            /*
            Components.ForEach(c => c.Dispose());
            Components.Clear();
            */
            _clippingRi.Dispose();
            _clippingRi = null;
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        public override bool MouseDown(MouseButtonEventArgs e)
        {
            if (Disposing || !Visible) return false;

            if (ScrollbarH.MouseDown(e))
            {
                SetFocus(ScrollbarH);
                return true;
            }
            if (ScrollbarV.MouseDown(e))
            {
                SetFocus(ScrollbarV);
                return true;
            }

            // since the RT is constructed at {0,0}, and then blitted to an offset position
            // we have to offset the mouse screen pos by the blit offset.
            var rtArgs = new MouseButtonEventArgs(e.Button, e.Position - Position);
            if (Container.MouseDown(rtArgs))
                return true;

            if (ClientArea.Translated(Position).Contains(e.X, e.Y))
            {
                var pos = new Vector2i(e.X - Position.X + (int) ScrollbarH.Value,
                    e.Y - Position.Y + (int) ScrollbarV.Value);

                var modArgs = new MouseButtonEventArgs(e.Button, pos);

                foreach (var component in Components)
                {
                    if (component.MouseDown(modArgs))
                    {
                        SetFocus(component);
                        return true;
                    }
                }
                return true;
            }

            return false;
        }

        public override bool MouseUp(MouseButtonEventArgs e)
        {
            if (Disposing || !Visible) return false;
            if (ScrollbarH.MouseUp(e)) return true;
            if (ScrollbarV.MouseUp(e)) return true;

            // since the RT is constructed at {0,0}, and then blitted to an offset position
            // we have to offset the mouse screen pos by the blit offset.
            var rtArgs = new MouseButtonEventArgs(e.Button, e.Position - Position);
            if (Container.MouseUp(rtArgs))
                return true;

            if (ClientArea.Translated(Position).Contains(e.X, e.Y))
            {
                var pos = new Vector2i(e.X - (Position.X + (int) ScrollbarH.Value),
                    e.Y - (Position.Y + (int) ScrollbarV.Value));

                var modArgs = new MouseButtonEventArgs(e.Button, pos);

                foreach (var component in Components)
                {
                    component.MouseUp(modArgs);
                }
            }
            return false;
        }

        public override void MouseMove(MouseMoveEventArgs e)
        {
            if (Disposing || !Visible) return;
            ScrollbarH.MouseMove(e);
            ScrollbarV.MouseMove(e);

            // since the RT is constructed at {0,0}, and then blitted to an offset position
            // we have to offset the mouse screen pos by the blit offset.
            var rtArgs = new MouseMoveEventArgs(e.NewPosition - Position);
            Container.MouseMove(rtArgs);

            var pos = new Vector2i(e.X - (Position.X + (int) ScrollbarH.Value),
                e.Y - (Position.Y + (int) ScrollbarV.Value));
            var modArgs = new MouseMoveEventArgs(pos);

            foreach (var component in Components)
            {
                component.MouseMove(modArgs);
            }
        }

        public override bool MouseWheelMove(MouseWheelScrollEventArgs e)
        {
            if (_innerFocus != null)
            {
                if (_innerFocus.MouseWheelMove(e))
                    return true;
                if (ScrollbarV.Visible && ClientArea.Contains(e.X, e.Y))
                {
                    ScrollbarV.MouseWheelMove(e);
                    return true;
                }
            }
            else if (ScrollbarV.Visible && ClientArea.Contains(e.X, e.Y))
            {
                ScrollbarV.MouseWheelMove(e);
                return true;
            }
            return false;
        }

        public override bool KeyDown(KeyEventArgs e)
        {
            foreach (var component in Components)
            {
                if (component.KeyDown(e)) return true;
            }
            return false;
        }

        public override bool TextEntered(TextEventArgs e)
        {
            foreach (var component in Components)
            {
                if (component.TextEntered(e)) return true;
            }
            return false;
        }

        public void ResetScrollbars()
        {
            if (ScrollbarH.Visible) ScrollbarH.Value = 0;
            if (ScrollbarV.Visible) ScrollbarV.Value = 0;
        }

        private void SetFocus(Control newFocus)
        {
            if (_innerFocus != null)
            {
                _innerFocus.Focus = false;
                _innerFocus = newFocus;
                newFocus.Focus = true;
            }
            else
            {
                _innerFocus = newFocus;
                newFocus.Focus = true;
            }
        }

        private void ClearFocus()
        {
            if (_innerFocus != null)
            {
                _innerFocus.Focus = false;
                _innerFocus = null;
            }
        }

        public class UiAnchor : Screen
        {
            private Vector2i _layoutScrPos = Vector2i.Zero;
            private Vector2i _scrollScrOffset = Vector2i.Zero;

            /// <summary>
            ///     Offset in px of screen position.
            /// </summary>
            public Vector2i ScrollOffset
            {
                get => _scrollScrOffset;
                set
                {
                    _scrollScrOffset = value;
                    Position = _layoutScrPos + _scrollScrOffset;
                    foreach (var control in Children)
                    {
                        control.DoLayout();
                    }
                }
            }

            protected override void OnCalcPosition()
            {
                base.OnCalcPosition();

                _layoutScrPos = Position;
                Position += ScrollOffset;
            }
        }
    }
}

﻿using SS14.Client.Graphics.Input;
using SS14.Client.Graphics.VertexData;
using SS14.Client.Interfaces.Resource;
using SS14.Shared.Maths;
using Vector2 = SS14.Shared.Maths.Vector2;
using Vector2i = SS14.Shared.Maths.Vector2i;

namespace SS14.Client.UserInterface.Components
{
    internal class Window : ScrollableContainer
    {
        protected const int titleBuffer = 1;

        public Color TitleColor1 = new Color(112, 128, 144);
        public Color TitleColor2 = new Color(47, 79, 79);

        protected ImageButton closeButton;
        public bool closeButtonVisible = true;
        protected bool dragging = false;
        protected Vector2 draggingOffset = new Vector2();
        protected GradientBox gradient;
        protected Label title;
        protected Box2i titleArea;

        public Window(string windowTitle, Vector2i size, IResourceCache resourceCache)
            : base(windowTitle, size, resourceCache)
        {
            closeButton = new ImageButton
            {
                ImageNormal = "closewindow"
            };

            closeButton.Clicked += CloseButtonClicked;
            title = new Label(windowTitle, "CALIBRI", _resourceCache);
            gradient = new GradientBox();
            DrawBackground = true;
            Update(0);
        }

        virtual protected void CloseButtonClicked(ImageButton sender)
        {
            Dispose();
        }

        public override void Update(float frameTime)
        {
            if (disposing || !IsVisible()) return;
            base.Update(frameTime);
            if (title == null || gradient == null) return;
            int y_pos = ClientArea.Top - (2 * titleBuffer) - title.ClientArea.Height + 1;
            title.Position = new Vector2i(ClientArea.Left + 3, y_pos + titleBuffer);
            titleArea = Box2i.FromDimensions(ClientArea.Left, y_pos, ClientArea.Width, title.ClientArea.Height + (2 * titleBuffer));
            title.Update(frameTime);
            closeButton.Position = new Vector2i(titleArea.Right - 5 - closeButton.ClientArea.Width,
                                             titleArea.Top + (int)(titleArea.Height / 2f) -
                                             (int)(closeButton.ClientArea.Height / 2f));
            gradient.ClientArea = titleArea;
            gradient.Color1 = TitleColor1;
            gradient.Color2 = TitleColor2;
            gradient.Update(frameTime);
            closeButton.Update(frameTime);
        }

        public override void Render() // Renders the main window
        {
            if (disposing || !IsVisible()) return;
            gradient.Render();

            //TODO RenderTargetRectangle
            base.Render();
            title.Render();
            if (closeButtonVisible) closeButton.Render();
        }

        public override void Dispose()
        {
            if (disposing) return;
            base.Dispose();
        }

        public override bool MouseDown(MouseButtonEventArgs e)
        {
            if (disposing || !IsVisible()) return false;

            if (closeButton.MouseDown(e)) return true;

            if (base.MouseDown(e)) return true;

            if (titleArea.Contains((int)e.X, (int)e.Y))
            {
                draggingOffset = new Vector2(e.X, e.Y) - Position;
                dragging = true;
                return true;
            }

            return false;
        }

        public override bool MouseUp(MouseButtonEventArgs e)
        {
            if (dragging) dragging = false;
            if (disposing || !IsVisible()) return false;
            if (base.MouseUp(e)) return true;

            return false;
        }

        public override void MouseMove(MouseMoveEventArgs e)
        {
            if (disposing || !IsVisible()) return;
            if (dragging)
            {
                Position = new Vector2i((int)e.X - (int)draggingOffset.X,
                                     (int)e.Y - (int)draggingOffset.Y);
            }
            base.MouseMove(e);

            return;
        }

        public override bool MouseWheelMove(MouseWheelScrollEventArgs e)
        {
            if (base.MouseWheelMove(e)) return true;
            return false;
        }

        public override bool KeyDown(KeyEventArgs e)
        {
            if (base.KeyDown(e)) return true;
            return false;
        }
    }

    public class GradientBox : GuiComponent
    {
        private readonly VertexTypeList.PositionDiffuse2DTexture1[] box =
            new VertexTypeList.PositionDiffuse2DTexture1[4];

        public Color Color1 = new Color(112, 128, 144);
        public Color Color2 = new Color(47, 79, 79);

        public bool Vertical = true;

        public override void Update(float frameTime)
        {
            box[0].Position.X = ClientArea.Left;
            box[0].Position.Y = ClientArea.Top;
            box[0].TextureCoordinates = Vector2.Zero;
            box[0].Color = Color1;

            box[1].Position.X = ClientArea.Right;
            box[1].Position.Y = ClientArea.Top;
            box[1].TextureCoordinates = Vector2.Zero;
            if (!Vertical) box[1].Color = Color2;
            else box[1].Color = Color1;

            box[2].Position.X = ClientArea.Right;
            box[2].Position.Y = ClientArea.Bottom;
            box[2].TextureCoordinates = Vector2.Zero;
            box[2].Color = Color2;

            box[3].Position.X = ClientArea.Left;
            box[3].Position.Y = ClientArea.Bottom;
            box[3].TextureCoordinates = Vector2.Zero;
            if (!Vertical) box[3].Color = Color1;
            else box[3].Color = Color2;
        }

        public override void Render()
        {
            //TODO Window Render
        }
    }
}

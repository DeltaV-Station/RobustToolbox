﻿using Robust.Shared.Maths;
using Robust.Shared.Utility;
﻿using Robust.Client.Graphics.Drawing;
using Robust.Client.Graphics.ClientEye;
using Robust.Client.Interfaces.Graphics;
using Robust.Client.Interfaces.Graphics.ClientEye;
using Robust.Shared.IoC;
using Robust.Shared.Map;

namespace Robust.Client.UserInterface.CustomControls
{
    /// <summary>
    ///     A viewport container shows a viewport.
    /// </summary>
    public class ViewportContainer : Control
    {
        private readonly IClyde _displayManager;
        public IClydeViewport Viewport = default!;

        private Vector2 _viewportResolution = (1.0f, 1.0f);

        /// <summary>
        ///     This controls the render target size, *as a fraction of the control size.*
        ///     Combined with controlling the Eye, this allows downscaling the game.
        /// </summary>
        public Vector2 ViewportResolution {
            get => _viewportResolution;
            set {
                _viewportResolution = value;
                Resized();
            }
        }

        public ViewportContainer()
        {
            _displayManager = IoCManager.Resolve<IClyde>();
            Resized();
        }

        // -- Handlers: Out --

        protected internal override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            Viewport.Render();
            if (Viewport == null)
            {
                handle.DrawRect(UIBox2.FromDimensions((0, 0), Size * UserInterfaceManager.UIScale), Color.Red);
            }
            else
            {
                handle.DrawTextureRect(Viewport.RenderTarget.Texture, UIBox2.FromDimensions((0, 0), (Vector2i) (Viewport.Size / _viewportResolution)));
            }
        }

        protected override void Resized()
        {
            Viewport?.Dispose();
            Viewport = _displayManager.CreateViewport(Vector2i.ComponentMax((1, 1), (Vector2i) (PixelSize * _viewportResolution)), "ViewportContainerViewport");
        }

        // -- Handlers: In --

        // -- Utils / S2M-M2S Base --

        public MapCoordinates LocalPixelToMap(Vector2 point)
        {
            if (Viewport.Eye == null)
                return MapCoordinates.Nullspace;

            var eye = (IEye) Viewport.Eye;
            var newPoint = point;

            // prescaler
            newPoint *= _viewportResolution;

            // (inlined version of UiProjMatrix^-1)
            newPoint -= Viewport.Size / 2f;
            newPoint *= new Vector2(1, -1) / EyeManager.PixelsPerMeter;

            // view matrix
            eye.GetViewMatrixInv(out var viewMatrixInv);
            newPoint = viewMatrixInv * newPoint;

            return new MapCoordinates(newPoint, eye.Position.MapId);
        }

        public Vector2 WorldToLocalPixel(Vector2 point)
        {
            if (Viewport.Eye == null)
                return (0, 0);

            var eye = (IEye) Viewport.Eye;
            var newPoint = point;

            eye.GetViewMatrix(out var viewMatrix);
            newPoint = viewMatrix * newPoint;

            // (inlined version of UiProjMatrix)
            newPoint *= new Vector2(1, -1) * EyeManager.PixelsPerMeter;
            newPoint += Viewport.Size / 2f;

            // prescaler
            newPoint /= _viewportResolution;

            return newPoint;
        }

        // -- Utils / S2M-M2S Extended --

        public MapCoordinates ScreenToMap(Vector2 point)
        {
            return LocalPixelToMap(point - GlobalPixelPosition);
        }

        public Vector2 WorldToScreen(Vector2 point)
        {
            return WorldToLocalPixel(point) + GlobalPixelPosition;
        }
    }
}

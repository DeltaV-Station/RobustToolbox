﻿using SFML.System;
using SFML.Graphics;
using SS14.Shared.Maths;
using SS14.Client.Graphics.Render;
using SS14.Client.Graphics.Utility;
using SView = SFML.Graphics.View;
using System;

namespace SS14.Client.Graphics.View
{
    public class Camera
    {
        public readonly CluwneWindow Window;
        internal readonly SView view;

        public Camera(CluwneWindow window)
        {
            Window = window;
            window.Resized += WindowResized;
            view = new SView(new FloatRect(0, 0, window.Width, window.Height));
            UpdateView();
        }

        public int PixelsPerMeter { get; } = 32;
        private Vector2 position;
        public Vector2 Position
        {
            get => position;
            set
            {
                position = value;
                view.Center = value.Convert() * PixelsPerMeter;
                UpdateView();
            }
        }

        private void WindowResized(object sender, SizeEventArgs args)
        {
            view.Viewport = new FloatRect(0, 0, args.Width, args.Height);
            UpdateView();
        }

        public void UpdateView()
        {
            Window.SFMLTarget.SetView(view);
        }
    }
}

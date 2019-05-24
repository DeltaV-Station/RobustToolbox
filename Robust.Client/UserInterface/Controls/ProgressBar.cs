﻿using System;
using System.Diagnostics.Contracts;
using Robust.Client.Graphics.Drawing;
using Robust.Shared.Maths;

namespace Robust.Client.UserInterface.Controls
{
    [ControlWrap("ProgressBar")]
    public class ProgressBar : Range
    {
        public const string StylePropertyBackground = "background";
        public const string StylePropertyForeground = "foreground";

        private StyleBox _backgroundStyleBoxOverride;
        private StyleBox _foregroundStyleBoxOverride;

        public ProgressBar()
        {
        }

        public ProgressBar(string name) : base(name)
        {
        }

        public StyleBox BackgroundStyleBoxOverride
        {
            get => _backgroundStyleBoxOverride;
            set
            {
                _backgroundStyleBoxOverride = value;
                MinimumSizeChanged();
            }
        }

        public StyleBox ForegroundStyleBoxOverride
        {
            get => _foregroundStyleBoxOverride;
            set
            {
                _foregroundStyleBoxOverride = value;
                MinimumSizeChanged();
            }
        }

        [Pure]
        private StyleBox _getBackground()
        {
            if (BackgroundStyleBoxOverride != null)
            {
                return BackgroundStyleBoxOverride;
            }

            TryGetStyleProperty(StylePropertyBackground, out StyleBox ret);
            return ret;
        }

        [Pure]
        private StyleBox _getForeground()
        {
            if (ForegroundStyleBoxOverride != null)
            {
                return ForegroundStyleBoxOverride;
            }

            TryGetStyleProperty(StylePropertyForeground, out StyleBox ret);
            return ret;
        }

        protected internal override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            var bg = _getBackground();
            bg?.Draw(handle, SizeBox);

            var fg = _getForeground();
            if (fg == null)
            {
                return;
            }
            var minSize = fg.MinimumSize;
            var size = Width * GetAsRatio() - minSize.X;
            if (size > 0)
            {
                fg.Draw(handle, UIBox2.FromDimensions(0, 0, minSize.X + size, Height));
            }
        }

        protected override Vector2 CalculateMinimumSize()
        {
            var bgSize = _getBackground()?.MinimumSize ?? Vector2.Zero;
            var fgSize = _getForeground()?.MinimumSize ?? Vector2.Zero;

            return Vector2.ComponentMax(bgSize, fgSize);
        }
    }
}

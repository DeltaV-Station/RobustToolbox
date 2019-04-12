﻿using System;
using Robust.Client.Graphics;
using Robust.Client.Graphics.Drawing;
using Robust.Client.Utility;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Robust.Client.UserInterface.Controls
{
    /// <summary>
    ///     A label is a GUI control that displays simple text.
    /// </summary>
    [ControlWrap(typeof(Godot.Label))]
    public class Label : Control
    {
        public const string StylePropertyFontColor = "font-color";
        public const string StylePropertyFont = "font";

        private Vector2i? _textDimensionCache;

        public Label(string name) : base(name)
        {
        }

        public Label() : base()
        {
        }

        internal Label(Godot.Label control) : base(control)
        {
        }

        private string _text;

        [ViewVariables]
        public string Text
        {
            get => GameController.OnGodot ? (string) SceneControl.Get("text") : _text;
            set
            {
                if (GameController.OnGodot)
                {
                    SceneControl.Set("text", value);
                }
                else
                {
                    _text = value;
                    _textDimensionCache = null;
                    MinimumSizeChanged();
                }
            }
        }

        [ViewVariables]
        public bool AutoWrap
        {
            get => GameController.OnGodot ? (bool) SceneControl.Get("autowrap") : default;
            set
            {
                if (GameController.OnGodot)
                {
                    SceneControl.Set("autowrap", value);
                }
            }
        }

        private AlignMode _align;

        [ViewVariables]
        public AlignMode Align
        {
            get => GameController.OnGodot ? (AlignMode) SceneControl.Get("align") : _align;
            set
            {
                if (GameController.OnGodot)
                {
                    SceneControl.Set("align", (Godot.Label.AlignEnum) value);
                }
                else
                {
                    _align = value;
                }
            }
        }

        private VAlignMode _vAlign;

        [ViewVariables]
        public VAlignMode VAlign
        {
            // ReSharper disable once StringLiteralTypo
            get => GameController.OnGodot ? (VAlignMode) SceneControl.Get("valign") : _vAlign;
            set
            {
                if (GameController.OnGodot)
                {
                    // ReSharper disable once StringLiteralTypo
                    SceneControl.Set("valign", (Godot.Label.VAlign) value);
                }
                else
                {
                    _vAlign = value;
                }
            }
        }

        private Font _fontOverride;

        public Font FontOverride
        {
            get => _fontOverride ?? GetFontOverride("font");
            set => SetFontOverride("font", _fontOverride = value);
        }

        private Font ActualFont
        {
            get
            {
                if (_fontOverride != null)
                {
                    return _fontOverride;
                }

                if (TryGetStyleProperty<Font>(StylePropertyFont, out var font))
                {
                    return font;
                }

                return UserInterfaceManager.ThemeDefaults.LabelFont;
            }
        }

        private Color? _fontColorShadowOverride;

        public Color? FontColorShadowOverride
        {
            get => _fontColorShadowOverride ?? GetColorOverride("font_color_shadow");
            set => SetColorOverride("font_color_shadow", _fontColorShadowOverride = value);
        }

        private Color ActualFontColor
        {
            get
            {
                if (_fontColorOverride.HasValue)
                {
                    return _fontColorOverride.Value;
                }

                if (TryGetStyleProperty<Color>(StylePropertyFontColor, out var color))
                {
                    return color;
                }

                return Color.White;
            }
        }

        private Color? _fontColorOverride;

        public Color? FontColorOverride
        {
            get => _fontColorOverride ?? GetColorOverride("font_color");
            set => SetColorOverride("font_color", _fontColorOverride = value);
        }

        private int? _shadowOffsetXOverride;

        public int? ShadowOffsetXOverride
        {
            get => _shadowOffsetXOverride ?? GetConstantOverride("shadow_offset_x");
            set => SetConstantOverride("shadow_offset_x", _shadowOffsetXOverride = value);
        }

        private int? _shadowOffsetYOverride;

        public int? ShadowOffsetYOverride
        {
            get => _shadowOffsetYOverride ?? GetConstantOverride("shadow_offset_y");
            set => SetConstantOverride("shadow_offset_y", _shadowOffsetYOverride = value);
        }

        private protected override Godot.Control SpawnSceneControl()
        {
            return new Godot.Label();
        }

        protected internal override void Draw(DrawingHandleScreen handle)
        {
            if (GameController.OnGodot)
            {
                return;
            }

            if (_text == null)
            {
                return;
            }

            if (!_textDimensionCache.HasValue)
            {
                _calculateTextDimension();
                DebugTools.Assert(_textDimensionCache.HasValue);
            }

            int hOffset;
            switch (Align)
            {
                case AlignMode.Left:
                    hOffset = 0;
                    break;
                case AlignMode.Center:
                case AlignMode.Fill:
                    hOffset = (int) (Size.X - _textDimensionCache.Value.X) / 2;
                    break;
                case AlignMode.Right:
                    hOffset = (int) (Size.X - _textDimensionCache.Value.X);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            int vOffset;
            switch (VAlign)
            {
                case VAlignMode.Top:
                    vOffset = 0;
                    break;
                case VAlignMode.Fill:
                case VAlignMode.Center:
                    vOffset = (int) (Size.Y - _textDimensionCache.Value.Y) / 2;
                    break;
                case VAlignMode.Bottom:
                    vOffset = (int) (Size.Y - _textDimensionCache.Value.Y);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var newlines = 0;
            var font = ActualFont;
            var baseLine = new Vector2(hOffset, font.Ascent + vOffset);
            var actualFontColor = ActualFontColor;
            foreach (var chr in _text)
            {
                if (chr == '\n')
                {
                    newlines += 1;
                    baseLine = new Vector2(hOffset, font.Ascent + font.LineHeight * newlines);
                }

                var advance = font.DrawChar(handle, chr, baseLine, actualFontColor);
                baseLine += new Vector2(advance, 0);
            }
        }

        public enum AlignMode
        {
            Left = 0,
            Center = 1,
            Right = 2,
            Fill = 3,
        }

        public enum VAlignMode
        {
            Top = 0,
            Center = 1,
            Bottom = 2,
            Fill = 3,
        }

        protected override Vector2 CalculateMinimumSize()
        {
            if (GameController.OnGodot)
            {
                return base.CalculateMinimumSize();
            }

            if (!_textDimensionCache.HasValue)
            {
                _calculateTextDimension();
                DebugTools.Assert(_textDimensionCache.HasValue);
            }

            return _textDimensionCache.Value;
        }

        private void _calculateTextDimension()
        {
            if (_text == null)
            {
                _textDimensionCache = Vector2i.Zero;
                return;
            }

            var font = ActualFont;
            var height = font.Height;
            var maxLineSize = 0;
            var currentLineSize = 0;
            foreach (var chr in _text)
            {
                if (chr == '\n')
                {
                    maxLineSize = Math.Max(currentLineSize, maxLineSize);
                    currentLineSize = 0;
                    height += font.LineHeight;
                }
                else
                {
                    var metrics = font.GetCharMetrics(chr);
                    if (!metrics.HasValue)
                    {
                        continue;
                    }

                    currentLineSize += metrics.Value.Advance;
                }
            }

            maxLineSize = Math.Max(currentLineSize, maxLineSize);

            _textDimensionCache = new Vector2i(maxLineSize, height);
        }

        protected override void StylePropertiesChanged()
        {
            _textDimensionCache = null;

            base.StylePropertiesChanged();
        }

        private protected override void SetGodotProperty(string property, object value, GodotAssetScene context)
        {
            base.SetGodotProperty(property, value, context);

            switch (property)
            {
                case "text":
                    Text = (string) value;
                    break;
                case "align":
                    Align = (AlignMode) (long) value;
                    break;
                // ReSharper disable once StringLiteralTypo
                case "valign":
                    VAlign = (VAlignMode) (long) value;
                    break;
            }
        }

        protected override void SetDefaults()
        {
            base.SetDefaults();
            MouseFilter = MouseFilterMode.Ignore;
            SizeFlagsVertical = SizeFlags.ShrinkCenter;
        }
    }
}

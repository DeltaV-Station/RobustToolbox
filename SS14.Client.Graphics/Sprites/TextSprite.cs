﻿using SFML.Graphics;
using SFML.System;
using OpenTK;
using OpenTK.Graphics;
using SS14.Client.Graphics.Utility;
using SS14.Shared.Utility;
using Vector2i = SS14.Shared.Maths.Vector2i;
using SS14.Shared.Maths;
using Vector2 = SS14.Shared.Maths.Vector2;
using Color = SS14.Shared.Maths.Color;
using SS14.Client.Graphics.Render;
using System;
using STransformable = SFML.Graphics.Transformable;
using RenderStates = SS14.Client.Graphics.Render.RenderStates;

namespace SS14.Client.Graphics.Sprites
{
    /// <summary>
    /// Sprite that contains Text
    /// </summary>
    public class TextSprite : Transformable, IDrawable
    {
        [Flags]
        public enum Styles
        {
            None = 0,
            Bold = 1 << 0,
            Italic = 1 << 1,
            Underlined = 1 << 2,
            StrikeThrough = 1 << 3,
        }

        public Text SFMLTextSprite { get; }

        public Drawable SFMLDrawable => SFMLTextSprite;
        public override STransformable SFMLTransformable => SFMLTextSprite;

        #region Constructors

        /// <summary>
        /// Creates a TextSprite
        /// </summary>
        /// <param name="Label"> Label of TextSprite </param>
        /// <param name="text"> Text to display </param>
        /// <param name="font"> Font to use when displaying Text </param>
        /// <param name="font"> Size of the font to use </param>
        public TextSprite(string text, Font font, uint size)
        {
            SFMLTextSprite = new Text(text, font.SFMLFont, size);
        }

        /// <summary>
        /// Creates a TextSprite
        /// </summary>
        /// <param name="Label"> Label of TextSprite </param>
        /// <param name="text"> Text to display </param>
        /// <param name="font"> Font to use when displaying Text </param>
        public TextSprite(string text, Font font) : this(text, font, 14) { }

        /// <summary>
        /// Draws the TextSprite to the CurrentRenderTarget
        /// </summary>
        ///

        #endregion Constructors

        #region Methods

        public void Draw(IRenderTarget target, RenderStates states)
        {
            SFMLTextSprite.Position = new Vector2f(Position.X, Position.Y);
            SFMLTextSprite.FillColor = FillColor.Convert();
            SFMLTextSprite.Draw(target.SFMLTarget, states.SFMLRenderStates);

            if (CluwneLib.Debug.DebugTextboxes)
            {
                var fr = SFMLTextSprite.GetGlobalBounds().Convert();
                CluwneLib.drawHollowRectangle((int)fr.Left, (int)fr.Top, (int)fr.Width, (int)fr.Height, 1.0f, Color.Red);
            }
        }

        public void Draw()
        {
            Draw(CluwneLib.CurrentRenderTarget, CluwneLib.ShaderRenderState);
        }

        /// <summary>
        /// Get the length, in pixels, that the provided string would be.
        /// </summary>
        public int MeasureLine(string _text)
        {
            string temp = Text;
            Text = _text;
            int value = (int)SFMLTextSprite.FindCharacterPos((uint)SFMLTextSprite.DisplayedString.Length + 1).X;
            Text = temp;
            return value;
        }

        /// <summary>
        /// Get the length, in pixels, of this TextSprite.
        /// </summary>
        public int MeasureLine()
        {
            return MeasureLine(Text);
        }

        public Vector2 FindCharacterPos(uint index)
        {
            return SFMLTextSprite.FindCharacterPos(index).Convert();
        }

        #endregion Methods

        #region Accessors

        public Color ShadowColor { get; set; }
        public bool Shadowed { get; set; } = false;
        public Vector2 ShadowOffset { get; set; }

        public Color FillColor
        {
            get => SFMLTextSprite.FillColor.Convert();
            set => SFMLTextSprite.FillColor = value.Convert();
        }

        public uint FontSize
        {
            get => SFMLTextSprite.CharacterSize;
            set => SFMLTextSprite.CharacterSize = value;
        }

        public string Text
        {
            get => SFMLTextSprite.DisplayedString;
            set => SFMLTextSprite.DisplayedString = value;
        }

        public Styles Style
        {
            get => (Styles)SFMLTextSprite.Style;
            set => SFMLTextSprite.Style = (SFML.Graphics.Text.Styles)value;
        }

        public int Width
        {
            get
            {
                var a = SFMLTextSprite;
                var b = a.GetLocalBounds();
                var c = b.Width;
                var d = (int)c;
                return d;
            }
        }
        // FIXME take into account newlines.
        public int Height => (int)SFMLTextSprite.CharacterSize;
        #endregion Accessors
    }
}

using System;
using System.Numerics;
using JetBrains.Annotations;
using Robust.Shared.Maths;

namespace Robust.Client.Graphics
{
    /// <summary>
    ///     This is for drawing modestly fancy boxes using minimal code.
    /// </summary>
    [PublicAPI]
    public abstract class StyleBox
    {
        private float? _contentMarginTopOverride;
        private float? _contentMarginLeftOverride;
        private float? _contentMarginBottomOverride;
        private float? _contentMarginRightOverride;

        private float _paddingLeft;
        private float _paddingBottom;
        private float _paddingRight;
        private float _paddingTop;

        protected StyleBox()
        {
        }

        protected StyleBox(StyleBox other)
        {
            _contentMarginBottomOverride = other._contentMarginBottomOverride;
            _contentMarginTopOverride = other._contentMarginTopOverride;
            _contentMarginLeftOverride = other._contentMarginLeftOverride;
            _contentMarginRightOverride = other._contentMarginRightOverride;

            _paddingLeft = other._paddingLeft;
            _paddingBottom = other._paddingBottom;
            _paddingRight = other._paddingRight;
            _paddingTop = other._paddingTop;
        }

        public Vector2 MinimumSize =>
            new(GetContentMargin(Margin.Left) + GetContentMargin(Margin.Right),
                GetContentMargin(Margin.Top) + GetContentMargin(Margin.Bottom));

        public float? ContentMarginLeftOverride
        {
            get => _contentMarginLeftOverride;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be less than zero.");
                }

                _contentMarginLeftOverride = value;
            }
        }

        public float? ContentMarginTopOverride
        {
            get => _contentMarginTopOverride;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be less than zero.");
                }

                _contentMarginTopOverride = value;
            }
        }

        public float? ContentMarginRightOverride
        {
            get => _contentMarginRightOverride;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be less than zero.");
                }

                _contentMarginRightOverride = value;
            }
        }

        public float? ContentMarginBottomOverride
        {
            get => _contentMarginBottomOverride;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be less than zero.");
                }

                _contentMarginBottomOverride = value;
            }
        }

        public float PaddingLeft
        {
            get => _paddingLeft;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be less than zero.");
                }

                _paddingLeft = value;
            }
        }

        public float PaddingBottom
        {
            get => _paddingBottom;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be less than zero.");
                }

                _paddingBottom = value;
            }
        }

        public float PaddingRight
        {
            get => _paddingRight;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be less than zero.");
                }

                _paddingRight = value;
            }
        }

        public float PaddingTop
        {
            get => _paddingTop;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be less than zero.");
                }

                _paddingTop = value;
            }
        }

        public Thickness Padding
        {
            set
            {
                PaddingLeft = value.Left;
                PaddingTop = value.Top;
                PaddingRight = value.Right;
                PaddingBottom = value.Bottom;
            }
        }

        /// <summary>
        ///     Draw this style box to the screen at the specified coordinates.
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="box"></param>
        [Obsolete("Use variant with a uiScale argument")]
        public void Draw(DrawingHandleScreen handle, UIBox2 box)
        {
            Draw(handle, box, 1.0f);
        }

        /// <summary>
        ///     Draw this style box to the screen at the specified coordinates.
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="box"></param>
        public void Draw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
        {
            box = new UIBox2(
                box.Left + PaddingLeft,
                box.Top + PaddingTop,
                box.Right - PaddingRight,
                box.Bottom - PaddingBottom
            );

            DoDraw(handle, box, uiScale);
        }

        /// <summary>
        ///     Gets the offset from a margin of the box to where content should actually be drawn.
        /// </summary>
        /// <exception cref="ArgumentException">
        ///     Thrown if <paramref name="margin"/> is a compound is not a single margin flag.
        /// </exception>
        public float GetContentMargin(Margin margin)
        {
            float? marginData;
            switch (margin)
            {
                case Margin.Top:
                    marginData = ContentMarginTopOverride;
                    break;
                case Margin.Bottom:
                    marginData = ContentMarginBottomOverride;
                    break;
                case Margin.Right:
                    marginData = ContentMarginRightOverride;
                    break;
                case Margin.Left:
                    marginData = ContentMarginLeftOverride;
                    break;
                default:
                    throw new ArgumentException("Margin must be a single margin flag.", nameof(margin));
            }

            var contentMargin = marginData ?? GetDefaultContentMargin(margin);
            return contentMargin + GetPadding(margin);
        }

        public float GetPadding(Margin margin)
        {
            switch (margin)
            {
                case Margin.Top:
                    return PaddingTop;
                case Margin.Bottom:
                    return PaddingBottom;
                case Margin.Right:
                    return PaddingRight;
                case Margin.Left:
                    return PaddingLeft;
                default:
                    throw new ArgumentException("Margin must be a single margin flag.", nameof(margin));
            }
        }

        /// <summary>
        ///     Returns the offsets of the content region of this box,
        ///     if this box is drawn from the given position.
        /// </summary>
        public Vector2 GetContentOffset(Vector2 basePosition)
        {
            return basePosition + new Vector2(GetContentMargin(Margin.Left), GetContentMargin(Margin.Top));
        }

        /// <summary>
        ///     Gets the box considered the "contents" of this style box, when drawn at a specific size.
        ///     If <paramRef name="uiScale"/> is not 1.0, it should be set to the value of the UIScale
        ///     property in the of the containing control and the <paramRef name="baseBox"/> should
        ///     have units of pixels (and the output will have pixel units.)
        /// </summary>
        /// <exception cref="ArgumentException">
        ///     <paramref name="baseBox"/> is too small and the resultant box would have negative dimensions.
        /// </exception>
        public UIBox2 GetContentBox(UIBox2 baseBox, float uiScale = 1.0f)
        {
            var left = baseBox.Left + GetContentMargin(Margin.Left) * uiScale;
            var top = baseBox.Top + GetContentMargin(Margin.Top) * uiScale;
            var right = baseBox.Right - GetContentMargin(Margin.Right) * uiScale;
            var bottom = baseBox.Bottom - GetContentMargin(Margin.Bottom) * uiScale;

            return new UIBox2(left, top, right, bottom);
        }

        /// <summary>
        ///     Gets the draw box, positioned at <paramref name="position"/>,
        ///     that envelops a box with the given dimensions perfectly given this box's content margins.
        ///     If <paramref name="uiScale"/> is not 1.0, it should be set to the UIScale property in the
        ///     control which contains this style box and the input (and output) coordinates are in
        ///     pixel units.
        /// </summary>
        /// <remarks>
        ///     It's basically a reverse <see cref="GetContentBox"/>.
        /// </remarks>
        /// <param name="position">The position at which the new box should be drawn.</param>
        /// <param name="dimensions">The dimensions of the content box inside this new box.</param>
        /// <param name="uiScale">Scales the content margin border size</param>
        /// <returns>
        ///     A box that, when ran through <see cref="GetContentBox"/>,
        ///     has a content box of size <paramref name="dimensions"/>
        /// </returns>
        public UIBox2 GetEnvelopBox(Vector2 position, Vector2 dimensions, float uiScale = 1.0f)
        {
            return UIBox2.FromDimensions(position, dimensions + MinimumSize * uiScale);
        }

        public void SetContentMarginOverride(Margin margin, float value)
        {
            if ((margin & Margin.Left) != 0)
            {
                ContentMarginLeftOverride = value;
            }

            if ((margin & Margin.Top) != 0)
            {
                ContentMarginTopOverride = value;
            }

            if ((margin & Margin.Right) != 0)
            {
                ContentMarginRightOverride = value;
            }

            if ((margin & Margin.Bottom) != 0)
            {
                ContentMarginBottomOverride = value;
            }
        }

        public void SetPadding(Margin margin, float value)
        {
            if ((margin & Margin.Left) != 0)
            {
                PaddingLeft = value;
            }

            if ((margin & Margin.Top) != 0)
            {
                PaddingTop = value;
            }

            if ((margin & Margin.Right) != 0)
            {
                PaddingRight = value;
            }

            if ((margin & Margin.Bottom) != 0)
            {
                PaddingBottom = value;
            }
        }

        protected abstract void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale);

        protected virtual float GetDefaultContentMargin(Margin margin)
        {
            return 0;
        }

        /// <summary>
        ///     Describes margins of a style box.
        /// </summary>
        [Flags]
        public enum Margin : byte
        {
            None = 0,

            /// <summary>
            ///     The top margin.
            /// </summary>
            Top = 1,

            /// <summary>
            ///     The bottom margin.
            /// </summary>
            Bottom = 2,

            /// <summary>
            ///     The right margin.
            /// </summary>
            Right = 4,

            /// <summary>
            ///     The left margin.
            /// </summary>
            Left = 8,

            /// <summary>
            ///     All margins.
            /// </summary>
            All = Top | Bottom | Right | Left,

            /// <summary>
            ///     The vertical margins.
            /// </summary>
            Vertical = Top | Bottom,

            /// <summary>
            ///     The horizontal margins.
            /// </summary>
            Horizontal = Left | Right,
        }
    }
}

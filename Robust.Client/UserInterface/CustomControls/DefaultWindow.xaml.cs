using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Robust.Client.UserInterface.CustomControls
{
    /// <summary>
    /// Simple window implementation that can be resized and has a title bar.
    /// </summary>
    /// <remarks>
    /// Warning: ugly.
    /// </remarks>
    [GenerateTypedNameReferences]
    [Virtual]
    // ReSharper disable once InconsistentNaming
    public partial class DefaultWindow : BaseWindow
    {
        public const string StyleClassWindowTitle = "windowTitle";
        public const string StyleClassWindowPanel = "windowPanel";
        public const string StyleClassWindowHeader = "windowHeader";
        public const string StyleClassWindowCloseButton = "windowCloseButton";

        /// <summary>
        ///     This ensures that windows are always visible on screen and don't become unreachable. This only applies
        ///     if the window is allowed to be partially off-screen.
        /// </summary>
        public float WindowEdgeSeparation = 30;

        /// <summary>
        ///     If a window is completely off-screen, the window will be bumped away from the edge so that its not a
        ///     tiny square hidden in a corner. Effectively a conditional increase to <see cref="WindowEdgeSeparation"/>.
        ///     Helpful when changing UI scale or changing the window size.
        /// </summary>
        public float WindowEdgeBump = 50;

        /// <summary>
        ///     This option determines whether a window is allowed to be dragged off the edge of the screen (as long as
        ///     part of it remains visible).
        /// </summary>
        /// <remarks>
        ///     Note that you generally want to disable north, as that will mean the header might not be visible, which
        ///     might render the window unmovable.
        /// </remarks>
        public DirectionFlag AllowOffScreen = ~DirectionFlag.North;

        private string? _headerClass;
        private string? _titleClass;

        public DefaultWindow()
        {
            RobustXamlLoader.Load(this);
            MouseFilter = MouseFilterMode.Stop;

            WindowHeader.MinSize = (0, HEADER_SIZE_Y);

            Contents = ContentsContainer;

            CloseButton.OnPressed += CloseButtonPressed;
            XamlChildren = new SS14ContentCollection(this);
        }

        public string? HeaderClass
        {
            get => _headerClass;
            set
            {
                if (_headerClass == value)
                    return;

                if (_headerClass != null)
                    WindowHeader.RemoveStyleClass(_headerClass);

                if (value != null)
                    WindowHeader.AddStyleClass(value);

                _headerClass = value;
            }
        }

        public string? TitleClass
        {
            get => _titleClass;
            set
            {
                if (_titleClass == value)
                    return;

                if (_titleClass != null)
                    TitleLabel.RemoveStyleClass(_titleClass);

                if (value != null)
                    TitleLabel.AddStyleClass(value);

                _titleClass = value;
            }
        }

        public Control Contents { get; private set; }

        private const int DRAG_MARGIN_SIZE = 7;

        // TODO: Un-hard code this header size.
        private const float HEADER_SIZE_Y = 25;
        protected virtual Vector2 ContentsMinimumSize => (50, 50);

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {

            return Vector2.ComponentMax(
                ContentsMinimumSize,
                base.MeasureOverride(Vector2.ComponentMax(availableSize, ContentsMinimumSize)));
        }

        public string? Title
        {
            get => TitleLabel.Text;
            set => TitleLabel.Text = value;
        }

        // Drag resizing and moving code is mostly taken from Godot's WindowDialog.

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                CloseButton.OnPressed -= CloseButtonPressed;
            }
        }

        private void CloseButtonPressed(BaseButton.ButtonEventArgs args)
        {
            Close();
        }

        // Prevent window headers from getting off screen due to game window resizes.

        protected override void FrameUpdate(FrameEventArgs args)
        {
            var (spaceX, spaceY) = Parent!.Size;

            var maxX = spaceX - ((AllowOffScreen & DirectionFlag.West) == 0 ? Size.X : WindowEdgeSeparation);
            var maxY = spaceY - ((AllowOffScreen & DirectionFlag.South) == 0 ? Size.Y : WindowEdgeSeparation);

            if (Position.X > spaceX)
                maxX -= WindowEdgeBump;

            if (Position.Y > spaceY)
                maxY -= WindowEdgeBump;

            var pos = Vector2.ComponentMin(Position, (maxX, maxY));

            var minX = (AllowOffScreen & DirectionFlag.East) ==  0 ? 0 : WindowEdgeSeparation - Size.X;
            var minY = (AllowOffScreen & DirectionFlag.North) == 0 ? 0 : WindowEdgeSeparation - Size.Y;

            pos = Vector2.ComponentMax(pos, (minX, minY));
            if (Position != pos)
                LayoutContainer.SetPosition(this, pos);
        }

        protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
        {
            var mode = DragMode.None;

            if (Resizable)
            {
                if (relativeMousePos.Y < DRAG_MARGIN_SIZE)
                {
                    mode = DragMode.Top;
                }
                else if (relativeMousePos.Y > Size.Y - DRAG_MARGIN_SIZE)
                {
                    mode = DragMode.Bottom;
                }

                if (relativeMousePos.X < DRAG_MARGIN_SIZE)
                {
                    mode |= DragMode.Left;
                }
                else if (relativeMousePos.X > Size.X - DRAG_MARGIN_SIZE)
                {
                    mode |= DragMode.Right;
                }
            }

            if (mode == DragMode.None && relativeMousePos.Y < HEADER_SIZE_Y)
            {
                mode = DragMode.Move;
            }

            return mode;
        }

        public sealed class SS14ContentCollection : ICollection<Control>, IReadOnlyCollection<Control>
        {
            private readonly DefaultWindow Owner;

            public SS14ContentCollection(DefaultWindow owner)
            {
                Owner = owner;
            }

            public Enumerator GetEnumerator()
            {
                return new(Owner);
            }

            IEnumerator<Control> IEnumerable<Control>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public void Add(Control item)
            {
                Owner.Contents.AddChild(item);
            }

            public void Clear()
            {
                Owner.Contents.RemoveAllChildren();
            }

            public bool Contains(Control item)
            {
                return item?.Parent == Owner.Contents;
            }

            public void CopyTo(Control[] array, int arrayIndex)
            {
                Owner.Contents.Children.CopyTo(array, arrayIndex);
            }

            public bool Remove(Control item)
            {
                if (item?.Parent != Owner.Contents)
                {
                    return false;
                }

                DebugTools.AssertNotNull(Owner?.Contents);
                Owner!.Contents.RemoveChild(item);

                return true;
            }

            int ICollection<Control>.Count => Owner.Contents.ChildCount;
            int IReadOnlyCollection<Control>.Count => Owner.Contents.ChildCount;

            public bool IsReadOnly => false;


            public struct Enumerator : IEnumerator<Control>
            {
                private OrderedChildCollection.Enumerator _enumerator;

                internal Enumerator(DefaultWindow DefaultWindow)
                {
                    _enumerator = DefaultWindow.Contents.Children.GetEnumerator();
                }

                public bool MoveNext()
                {
                    return _enumerator.MoveNext();
                }

                public void Reset()
                {
                    _enumerator.Reset();
                }

                public Control Current => _enumerator.Current;

                object IEnumerator.Current => Current;

                public void Dispose()
                {
                    _enumerator.Dispose();
                }
            }
        }
    }
}

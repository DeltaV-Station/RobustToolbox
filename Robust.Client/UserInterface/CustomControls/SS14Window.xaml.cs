using System.Collections;
using System.Collections.Generic;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Robust.Client.UserInterface.CustomControls
{
    [GenerateTypedNameReferences]
    // ReSharper disable once InconsistentNaming
    public partial class SS14Window : BaseWindow
    {
        public const string StyleClassWindowTitle = "windowTitle";
        public const string StyleClassWindowPanel = "windowPanel";
        public const string StyleClassWindowHeader = "windowHeader";
        public const string StyleClassWindowCloseButton = "windowCloseButton";

        public SS14Window()
        {
            RobustXamlLoader.Load(this);
            MouseFilter = MouseFilterMode.Stop;

            WindowHeader.MinSize = (0, HEADER_SIZE_Y);

            Contents = ContentsContainer;

            CloseButton.OnPressed += CloseButtonPressed;
            XamlChildren = new SS14ContentCollection(this);
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
            if (Position.Y > spaceY)
            {
                LayoutContainer.SetPosition(this, (Position.X, spaceY + HEADER_SIZE_Y));
            }

            if (Position.X > spaceX)
            {
                // 50 is arbitrary here. As long as it's bumped back into view.
                LayoutContainer.SetPosition(this, (spaceX - 50, Position.Y));
            }

            if (Position.Y < 0)
            {
                LayoutContainer.SetPosition(this, (Position.X, 0));
            }

            if (Position.X < 0)
            {
                LayoutContainer.SetPosition(this, (0, Position.Y));
            }
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

        public class SS14ContentCollection : ICollection<Control>, IReadOnlyCollection<Control>
        {
            private readonly SS14Window Owner;

            public SS14ContentCollection(SS14Window owner)
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

                internal Enumerator(SS14Window ss14Window)
                {
                    _enumerator = ss14Window.Contents.Children.GetEnumerator();
                }

                public bool MoveNext()
                {
                    return _enumerator.MoveNext();
                }

                public void Reset()
                {
                    ((IEnumerator) _enumerator).Reset();
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

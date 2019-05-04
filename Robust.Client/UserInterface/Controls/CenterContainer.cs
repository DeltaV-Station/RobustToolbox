using Robust.Shared.Maths;

namespace Robust.Client.UserInterface.Controls
{
    [ControlWrap("CenterContainer")]
    public class CenterContainer : Container
    {
        protected override void SortChildren()
        {
            foreach (var child in Children)
            {
                var childSize = child.CombinedMinimumSize;
                var childPos = (Size - childSize) / 2;

                FitChildInBox(child, UIBox2.FromDimensions(childPos, childSize));
            }
        }

        protected override Vector2 CalculateMinimumSize()
        {
            var min = Vector2.Zero;

            foreach (var child in Children)
            {
                min = Vector2.ComponentMax(child.CombinedMinimumSize, min);
            }

            return min;
        }
    }
}

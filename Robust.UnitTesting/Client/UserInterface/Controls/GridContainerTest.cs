using NUnit.Framework;
using Robust.Client.Interfaces.UserInterface;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.Utility;
using Robust.Shared.Interfaces.Resources;
using Robust.Shared.IoC;
using Robust.Shared.Utility;
using Robust.Shared.Maths;

namespace Robust.UnitTesting.Client.UserInterface.Controls
{
    [TestFixture]
    [TestOf(typeof(GridContainer))]
    public class GridContainerTest : RobustUnitTest
    {
        public override UnitTestProject Project => UnitTestProject.Client;

        [Test]
        public void TestBasic()
        {
            var grid = new GridContainer {Columns = 2};
            var child1 = new Control {CustomMinimumSize = (50, 50)};
            var child2 = new Control {CustomMinimumSize = (50, 50)};
            var child3 = new Control {CustomMinimumSize = (50, 50)};
            var child4 = new Control {CustomMinimumSize = (50, 50)};
            var child5 = new Control {CustomMinimumSize = (50, 50)};

            grid.AddChild(child1);
            grid.AddChild(child2);
            grid.AddChild(child3);
            grid.AddChild(child4);
            grid.AddChild(child5);

            Assert.That(grid.CombinedMinimumSize, Is.EqualTo(new Vector2(104, 158)));

            Assert.That(child1.Position, Is.EqualTo(Vector2.Zero));
            Assert.That(child2.Position, Is.EqualTo(new Vector2(54, 0)));
            Assert.That(child3.Position, Is.EqualTo(new Vector2(0, 54)));
            Assert.That(child4.Position, Is.EqualTo(new Vector2(54, 54)));
            Assert.That(child5.Position, Is.EqualTo(new Vector2(0, 108)));
        }

        [Test]
        public void TestExpand()
        {
            var grid = new GridContainer {Columns = 2, Size = (200, 200)};
            var child1 = new Control {CustomMinimumSize = (50, 50), SizeFlagsHorizontal = Control.SizeFlags.FillExpand};
            var child2 = new Control {CustomMinimumSize = (50, 50)};
            var child3 = new Control {CustomMinimumSize = (50, 50)};
            var child4 = new Control {CustomMinimumSize = (50, 50), SizeFlagsVertical = Control.SizeFlags.FillExpand};
            var child5 = new Control {CustomMinimumSize = (50, 50)};

            grid.AddChild(child1);
            grid.AddChild(child2);
            grid.AddChild(child3);
            grid.AddChild(child4);
            grid.AddChild(child5);

            Assert.That(child1.Position, Is.EqualTo(Vector2.Zero));
            Assert.That(child1.Size, Is.EqualTo(new Vector2(146, 50)));
            Assert.That(child2.Position, Is.EqualTo(new Vector2(150, 0)));
            Assert.That(child2.Size, Is.EqualTo(new Vector2(50, 50)));
            Assert.That(child3.Position, Is.EqualTo(new Vector2(0, 54)));
            Assert.That(child3.Size, Is.EqualTo(new Vector2(146, 92)));
            Assert.That(child4.Position, Is.EqualTo(new Vector2(150, 54)));
            Assert.That(child4.Size, Is.EqualTo(new Vector2(50, 92)));
            Assert.That(child5.Position, Is.EqualTo(new Vector2(0, 150)));
            Assert.That(child5.Size, Is.EqualTo(new Vector2(146, 50)));
        }

        [Test]
        public void TestRowCount()
        {
            var grid = new GridContainer {Columns = 2};
            var child1 = new Control {CustomMinimumSize = (50, 50)};
            var child2 = new Control {CustomMinimumSize = (50, 50)};
            var child3 = new Control {CustomMinimumSize = (50, 50)};
            var child4 = new Control {CustomMinimumSize = (50, 50)};
            var child5 = new Control {CustomMinimumSize = (50, 50)};

            grid.AddChild(child1);
            grid.AddChild(child2);
            grid.AddChild(child3);
            grid.AddChild(child4);
            grid.AddChild(child5);

            Assert.That(grid.Rows, Is.EqualTo(3));

            grid.RemoveChild(child5);

            Assert.That(grid.Rows, Is.EqualTo(2));

            grid.DisposeAllChildren();

            Assert.That(grid.Rows, Is.EqualTo(1));
        }
    }
}

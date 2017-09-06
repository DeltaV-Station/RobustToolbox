﻿using System.Collections.Generic;
using NUnit.Framework;
using OpenTK;
using SS14.Shared.Maths;
using Vector2 = SS14.Shared.Maths.Vector2;

namespace SS14.UnitTesting.Shared.Maths
{
    [TestFixture]
    public class Angle_Test
    {
        private const double Epsilon = 1.0e-8;

        private static IEnumerable<(float, float, Direction, double)> Sources => new(float, float, Direction, double)[]
        {
            (1, 0, Direction.East, 0.0),
            (1, 1, Direction.NorthEast, System.Math.PI / 4.0),
            (0, 1, Direction.North, System.Math.PI / 2.0),
            (-1, 1, Direction.NorthWest, 3 * System.Math.PI / 4.0),
            (-1, 0, Direction.West, System.Math.PI),
            (-1, -1, Direction.SouthWest, -3 * System.Math.PI / 4.0),
            (0, -1, Direction.South, -System.Math.PI / 2.0),
            (1, -1, Direction.SouthEast, -System.Math.PI / 4.0)
        };

        [Test]
        [Sequential]
        public void TestAngleToVector2([ValueSource(nameof(Sources))] (float, float, Direction, double) test)
        {
            var control = new Vector2(test.Item1, test.Item2).Normalized;
            var target = new Angle(test.Item4);

            Assert.That((control - target.ToVec()).LengthSquared, Is.AtMost(Epsilon));
        }

        [Test]
        [Sequential]
        public void TestAngleToDirection([ValueSource(nameof(Sources))] (float, float, Direction, double) test)
        {
            var target = new Angle(test.Item4);

            Assert.That(target.GetDir(), Is.EqualTo(test.Item3));
        }
    }
}

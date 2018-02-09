﻿using System.Collections.Generic;
using System.Configuration;
using NUnit.Framework;
using SS14.Shared.Maths;

namespace SS14.UnitTesting.Shared.Maths
{
    [TestFixture]
    [Parallelizable]
    [TestOf(typeof(Matrix3))]
    public class Matrix3_Test
    {
        [Test]
        public void TranslationTest()
        {
            var control = new Vector2(1, 1);
            var matrix = Matrix3.CreateTranslation(control);

            Vector3 origin = new Vector3(0, 0, 1);
            Matrix3.Transform(ref matrix, ref origin);

            Vector2 result = origin.Xy;
            Assert.That(control == result, Is.True);
        }

        private static readonly IEnumerable<(Vector2, double)> _rotationTests = new[]
        {
            (new Vector2( 1, 0).Normalized, 0.0),
            (new Vector2( 1, 1).Normalized, 1 * System.Math.PI / 4.0),
            (new Vector2( 0, 1).Normalized, 1 * System.Math.PI / 2.0),
            (new Vector2(-1, 1).Normalized, 3 * System.Math.PI / 4.0),
            (new Vector2(-1, 0).Normalized, 1 * System.Math.PI / 1.0),
            (new Vector2(-1,-1).Normalized, 5 * System.Math.PI / 4.0),
            (new Vector2( 0,-1).Normalized, 3 * System.Math.PI / 2.0),
            (new Vector2( 1,-1).Normalized, 7 * System.Math.PI / 4.0),
        };

        [Test]
        [Sequential]
        public void RotationTest([ValueSource(nameof(_rotationTests))] (Vector2, double) testCase)
        {
            var angle = testCase.Item2;

            var matrix = Matrix3.CreateRotation((float) angle);
            
            Vector3 test = new Vector3(1, 0, 1);
            Matrix3.Transform(ref matrix, ref test);

            var control = testCase.Item1;
            var result = test.Xy;

            Assert.That(FloatMath.CloseTo(control.X, result.X), Is.True, result.ToString);
            Assert.That(FloatMath.CloseTo(control.Y, result.Y), Is.True, result.ToString);
        }

        [Test]
        public void MultiplyTest()
        {
            var startPoint = new Vector3(2, 0, 1);
            var rotateMatrix = Matrix3.CreateRotation((float) (System.Math.PI / 2.0)); // 1. rotate 90 degrees upward
            var translateMatrix = Matrix3.CreateTranslation(new Vector2(0, -2)); // 2. translate 0, -2 downwards.

            // NOTE: Matrix Product is NOT commutative. OpenTK (and this) uses pre-multiplication, OpenGL and all the tutorials
            // you will read about it use post-multiplication. So in OpenTK MVP = M*V*P; in OpenGL it is MVP = P*V*M.
            rotateMatrix.Multiply(ref translateMatrix, out var transformMatrix);

            Vector3 result = startPoint;
            Matrix3.Transform(ref transformMatrix, ref result);

            Assert.That(FloatMath.CloseTo(0, result.X), Is.True, result.ToString);
            Assert.That(FloatMath.CloseTo(0, result.Y), Is.True, result.ToString);
        }

        [Test]
        public void InvertTest()
        {
            // take our matrix
            var normalMatrix = new Matrix3(
                3, 7, 2,
                1, 8, 4,
                2, 1, 9
                );

            // invert it (1/matrix)
            var invMatrix = Matrix3.Invert(normalMatrix);

            // multiply it back together
            invMatrix.Multiply(ref normalMatrix, out var verifyMatrix);

            const float epsilon = 1.0E-7f;
            var control = Matrix3.Identity;

            // verify matrix == identity matrix (or very close to because float precision)
            Assert.That(verifyMatrix.EqualsApprox(ref control, epsilon), Is.True, verifyMatrix.ToString);
        }
    }
}

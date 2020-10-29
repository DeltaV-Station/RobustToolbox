﻿using System;

namespace Robust.Shared.Maths
{
    /// <summary>
    ///     This type contains a <see cref="Box2"/> and a rotation <see cref="Angle"/> in world space.
    /// </summary>
    [Serializable]
    public struct Box2Rotated : IEquatable<Box2Rotated>
    {
        public Box2 Box;
        public Angle Rotation;
        /// <summary>
        /// The point about which the rotation occurs.
        /// </summary>
        public Vector2 Origin;

        /// <summary>
        ///     A 1x1 unit box with the origin centered and identity rotation.
        /// </summary>
        public static readonly Box2Rotated UnitCentered = new Box2Rotated(Box2.UnitCentered, Angle.Zero, Vector2.Zero);

        public readonly Vector2 BottomRight => Origin + Rotation.RotateVec(Box.BottomRight - Origin);
        public readonly Vector2 TopLeft => Origin + Rotation.RotateVec(Box.TopLeft - Origin);
        public readonly Vector2 TopRight => Origin + Rotation.RotateVec(Box.TopRight - Origin);
        public readonly Vector2 BottomLeft => Origin + Rotation.RotateVec(Box.BottomLeft - Origin);

        public Box2Rotated(Vector2 bottomLeft, Vector2 topRight)
            : this(new Box2(bottomLeft, topRight)) { }

        public Box2Rotated(Box2 box)
            : this(box, 0) { }

        public Box2Rotated(Box2 box, Angle rotation)
            : this(box, rotation, Vector2.Zero) { }

        public Box2Rotated(Box2 box, Angle rotation, Vector2 origin)
        {
            Box = box;
            Rotation = rotation;
            Origin = origin;
        }

        /// <summary>
        /// calculates the smallest AABB that will encompass the rotated box. The AABB is in local space.
        /// </summary>
        public readonly Box2 CalcBoundingBox()
        {
            // https://stackoverflow.com/a/19830964

            float[] allX = new float[4];
            float[] allY = new float[4];
            (allX[0], allY[0]) = BottomLeft;
            (allX[1], allY[1]) = TopRight;
            (allX[2], allY[2]) = TopLeft;
            (allX[3], allY[3]) = BottomRight;

            var X0 = allX[0];
            var X1 = allX[0];
            for (int i = 1; i < allX.Length; i++)
            {
                if (allX[i] > X1)
                {
                    X1 = allX[i];
                    continue;
                }

                if (allX[i] < X0)
                {
                    X0 = allX[i];
                }
            }

            var Y0 = allY[0];
            var Y1 = allY[0];
            for (int i = 1; i < allY.Length; i++)
            {
                if (allY[i] > Y1)
                {
                    Y1 = allY[i];
                    continue;
                }

                if (allY[i] < Y0)
                {
                    Y0 = allY[i];
                }
            }

            return new Box2(X0, Y0, X1, Y1);
        }

        #region Equality

        /// <inheritdoc />
        public readonly bool Equals(Box2Rotated other)
        {
            return Box.Equals(other.Box) && Rotation.Equals(other.Rotation);
        }

        /// <inheritdoc />
        public override readonly bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is Box2Rotated other && Equals(other);
        }

        /// <inheritdoc />
        public override readonly int GetHashCode()
        {
            unchecked
            {
                return (Box.GetHashCode() * 397) ^ Rotation.GetHashCode();
            }
        }

        /// <summary>
        ///     Check for equality by value between two <see cref="Box2Rotated"/>.
        /// </summary>
        public static bool operator ==(Box2Rotated a, Box2Rotated b)
        {
            return a.Equals(b);
        }

        /// <summary>
        ///     Check for inequality by value between two <see cref="Box2Rotated"/>.
        /// </summary>
        public static bool operator !=(Box2Rotated a, Box2Rotated b)
        {
            return !a.Equals(b);
        }

        #endregion

        /// <summary>
        ///     Returns a string representation of this type.
        /// </summary>
        public override readonly string ToString()
        {
            return $"{Box.ToString()}, {Rotation.ToString()}";
        }
    }
}

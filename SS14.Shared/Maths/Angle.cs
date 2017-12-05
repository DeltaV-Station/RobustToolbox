﻿using System;
using OpenTK;

namespace SS14.Shared.Maths
{
    /// <summary>
    ///     A representation of an angle, in radians.
    /// </summary>
    [Serializable]
    public struct Angle
    {
        private const double Segment = 2 * Math.PI / 8.0; // Cut the circle into 8 pieces
        private const double Offset = Segment / 2.0; // offset the pieces by 1/2 their size

        public static Angle Zero { get; set; } = new Angle();

        public readonly double Theta;
        public double Degrees => MathHelper.RadiansToDegrees(Theta);

        /// <summary>
        ///     Constructs an instance of an Angle.
        /// </summary>
        /// <param name="theta">The angle in radians.</param>
        public Angle(double theta)
        {
            Theta = theta;
        }

        /// <summary>
        ///     Converts this angle to a unit direction vector.
        /// </summary>
        /// <returns>Unit Direction Vector</returns>
        public Vector2 ToVec()
        {
            var x = Math.Cos(Theta);
            var y = Math.Sin(Theta);
            return new Vector2((float) x, (float) y);
        }

        public Direction GetDir()
        {
            var ang = Theta % (2 * Math.PI);

            if (ang < 0.0f) // convert -PI > PI to 0 > 2PI
                ang += 2 * (float) Math.PI;

            return (Direction) Math.Floor((ang + Offset) / Segment);
        }

        /// <summary>
        ///     Constructs a new angle, from degrees instead of radians.
        /// </summary>
        /// <param name="degrees">The angle in degrees.</param>
        public static Angle FromDegrees(double degrees)
        {
            return new Angle(MathHelper.DegreesToRadians(degrees));
        }

        /// <summary>
        ///     Implicit conversion from Angle to double.
        /// </summary>
        /// <param name="angle"></param>
        public static implicit operator double(Angle angle)
        {
            return angle.Theta;
        }

        /// <summary>
        ///     Implicit conversion from double to Angle.
        /// </summary>
        /// <param name="theta"></param>
        public static implicit operator Angle(double theta)
        {
            return new Angle(theta);
        }

        /// <summary>
        ///     Implicit conversion from float to Angle.
        /// </summary>
        /// <param name="theta"></param>
        public static implicit operator Angle(float theta)
        {
            return new Angle(theta);
        }
    }
}

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Robust.Shared.Maths;

public static class Vector2Helpers
{
    public static readonly Vector2 Infinity = new(float.PositiveInfinity, float.PositiveInfinity);
    public static readonly Vector2 NaN = new(float.NaN, float.NaN);

    /// <summary>
    /// Half of a unit vector.
    /// </summary>
    public static readonly Vector2 Half = new(0.5f, 0.5f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 InterpolateCubic(Vector2 preA, Vector2 a, Vector2 b, Vector2 postB, float t)
    {
        return a + (b - preA + (preA * 2.0f - a * 5.0f + b * 4.0f - postB + ((a - b) * 3.0f + postB - preA) * t) * t) * t * 0.5f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsApprox(this Vector2 vec, Vector2 otherVec)
    {
        return MathHelper.CloseTo(vec.X, otherVec.X) && MathHelper.CloseTo(vec.Y, otherVec.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsApprox(this Vector2 vec, Vector2 otherVec, double tolerance)
    {
        return MathHelper.CloseTo(vec.X, otherVec.X, tolerance) && MathHelper.CloseTo(vec.Y, otherVec.Y, tolerance);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Normalized(this Vector2 vec)
    {
        var length = vec.Length();
        return new Vector2(vec.X / length, vec.Y / length);
    }

    /// <summary>
    /// Normalizes this vector if its length > 0, otherwise sets it to 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Normalize(ref this Vector2 vec)
    {
        var length = vec.Length();

        if (length < float.Epsilon)
        {
            vec = Vector2.Zero;
            return 0f;
        }

        var invLength = 1f / length;
        vec.X *= invLength;
        vec.Y *= invLength;
        return length;
    }

    /// <summary>
    /// Perform the cross product on a scalar and a vector. In 2D this produces
    /// a vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Cross(float s, in Vector2 a)
    {
        return new(-s * a.Y, s * a.X);
    }

    /// <summary>
    /// Perform the cross product on a scalar and a vector. In 2D this produces
    /// a vector.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Cross(in Vector2 a, in float s)
    {
        return new(s * a.Y, -s * a.X);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Cross(Vector2 a, Vector2 b)
    {
        return a.X * b.Y - a.Y * b.X;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2i Floored(this Vector2 vec)
    {
        return new Vector2i((int) MathF.Floor(vec.X), (int) MathF.Floor(vec.Y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2i Ceiled(this Vector2 vec)
    {
        return new Vector2i((int) MathF.Ceiling(vec.X), (int) MathF.Ceiling(vec.Y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Rounded(this Vector2 vec)
    {
        return new Vector2(MathF.Round(vec.X), MathF.Round(vec.Y));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Deconstruct(this Vector2 vec, out float x, out float y)
    {
        x = vec.X;
        y = vec.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsApproxPercent(this Vector2 vec, Vector2 other, double tolerance = 0.0001)
    {
        return MathHelper.CloseToPercent(vec.X, other.X, tolerance) && MathHelper.CloseToPercent(vec.Y, other.Y, tolerance);
    }
}

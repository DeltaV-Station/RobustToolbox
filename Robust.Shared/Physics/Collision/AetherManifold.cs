using System;
using System.Runtime.InteropServices;
using Robust.Shared.Localization;
using Robust.Shared.Maths;

namespace Robust.Shared.Physics.Collision
{
    public enum ManifoldType : byte
    {
        Invalid = 0,
        Circles,
        FaceA,
        FaceB,
    }

    internal enum ContactFeatureType : byte
    {
        Vertex = 0,
        Face = 1,
    }

    /// <summary>
    /// The features that intersect to form the contact point
    /// This must be 4 bytes or less.
    /// </summary>
    public struct ContactFeature
    {
        /// <summary>
        /// Feature index on ShapeA
        /// </summary>
        public byte IndexA;

        /// <summary>
        /// Feature index on ShapeB
        /// </summary>
        public byte IndexB;

        /// <summary>
        /// The feature type on ShapeA
        /// </summary>
        public byte TypeA;

        /// <summary>
        /// The feature type on ShapeB
        /// </summary>
        public byte TypeB;
    }

    /// <summary>
    /// Contact ids to facilitate warm starting.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct ContactID
    {
        /// <summary>
        /// The features that intersect to form the contact point
        /// </summary>
        [FieldOffset(0)]
        public ContactFeature Features;

        /// <summary>
        /// Used to quickly compare contact ids.
        /// </summary>
        [FieldOffset(0)]
        public uint Key;

        public static bool operator ==(ContactID id, ContactID other)
        {
            return id.Key == other.Key;
        }

        public static bool operator !=(ContactID id, ContactID other)
        {
            return !(id == other);
        }
    }

    // Originally this was a struct but it gets mutated all over the place so I just made it a class for now.
    internal sealed class AetherManifold
    {
        public Vector2 LocalNormal;

        /// <summary>
        ///     Usage depends on manifold type.
        /// </summary>
        public Vector2 LocalPoint;

        public int PointCount;

        /// <summary>
        ///     Points of contact, can only be 0 -> 2.
        /// </summary>
        public ManifoldPoint[] Points = new ManifoldPoint[2];

        public ManifoldType Type;

        public AetherManifold() {}

        public AetherManifold(Vector2 localNormal, Vector2 localPoint, int pointCount, ManifoldPoint[] points, ManifoldType type)
        {
            LocalNormal = localNormal;
            LocalPoint = localPoint;
            PointCount = pointCount;
            Array.Copy(points, Points, pointCount);
            Type = type;
        }

        // TODO: Make a unittest for this real fucken bad.
        public AetherManifold Clone()
        {
            return new(LocalNormal, LocalPoint, PointCount, Points, Type);
        }
    }

    public struct ManifoldPoint
    {
        /// <summary>
        ///     Unique identifier for the contact point between 2 shapes.
        /// </summary>
        public ContactID Id;

        /// <summary>
        ///     Usage depends on manifold type.
        /// </summary>
        public Vector2 LocalPoint;

        /// <summary>
        ///     The non-penetration impulse.
        /// </summary>
        public float NormalImpulse;

        /// <summary>
        ///     Friction impulse.
        /// </summary>
        public float TangentImpulse;

        public static bool operator ==(ManifoldPoint point, ManifoldPoint other)
        {
            return point.Id == other.Id &&
                   point.LocalPoint.Equals(other.LocalPoint) &&
                   point.NormalImpulse.Equals(other.NormalImpulse) &&
                   point.TangentImpulse.Equals(other.TangentImpulse);
        }

        public static bool operator !=(ManifoldPoint point, ManifoldPoint other)
        {
            return !(point == other);
        }
    }
}

﻿using System;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Robust.Shared.Physics
{
    /// <summary>
    /// A physics shape that represents an Axis-Aligned Bounding Box.
    /// This box does not rotate with the entity, and will always be offset from the
    /// entity origin in world space.
    /// </summary>
    [Serializable, NetSerializable]
    public class PhysShapeAabb : IPhysShape
    {
        private Box2 _localBounds = Box2.UnitCentered;

        /// <summary>
        /// Local AABB bounds of this shape.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public Box2 LocalBounds
        {
            get => _localBounds;
            set
            {
                if (_localBounds == value)
                    return;

                _localBounds = value;
                OnDataChanged?.Invoke();
            }
        }

        /// <inheritdoc />
        public void ApplyState() { }

        public void DebugDraw(DebugDrawingHandle handle, in Matrix3 modelMatrix, in Box2 worldViewport,
            float sleepPercent)
        {
            var m = Matrix3.Identity;
            m.R0C2 = modelMatrix.R0C2;
            m.R1C2 = modelMatrix.R1C2;

            handle.SetTransform(m);
            handle.DrawRect(LocalBounds, handle.CalcWakeColor(handle.RectFillColor, sleepPercent));
            handle.SetTransform(Matrix3.Identity);
        }

        // TODO
        [field: NonSerialized]
        public event Action? OnDataChanged;

        /// <inheritdoc />
        public Box2 CalculateLocalBounds(Angle rotation)
        {
            return _localBounds;
        }

        /// <inheritdoc />
        public void ExposeData(ObjectSerializer serializer)
        {
            serializer.DataField(ref _localBounds, "bounds", Box2.UnitCentered);
        }

        public bool Equals(IPhysShape? other)
        {
            if (other is not PhysShapeAabb otherAABB) return false;
            return _localBounds.EqualsApprox(otherAABB._localBounds);
        }
    }
}

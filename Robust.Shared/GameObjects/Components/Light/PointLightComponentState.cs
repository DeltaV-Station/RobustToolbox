﻿using System;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Robust.Shared.GameObjects
{
    [Serializable, NetSerializable]
    public class PointLightComponentState : ComponentState
    {
        public readonly Color Color;
        public readonly bool Enabled;
        public readonly bool Occluded;

        public readonly float Radius;
        public readonly Vector2 Offset;

        public PointLightComponentState(bool enabled, bool occluded, Color color, float radius, Vector2 offset)
            : base(NetIDs.POINT_LIGHT)
        {
            Enabled = enabled;
            Occluded = occluded;
            Color = color;
            Radius = radius;
            Offset = offset;
        }
    }
}

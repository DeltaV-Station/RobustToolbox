﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;

namespace Robust.Shared.Interfaces.Physics
{
    /// <summary>
    ///     This service provides access into the physics system.
    /// </summary>
    public interface IPhysicsManager
    {
        /// <summary>
        ///     Checks whether a certain grid position is weightless or not
        /// </summary>
        /// <param name="coordinates"></param>
        /// <returns></returns>
        bool IsWeightless(EntityCoordinates coordinates);

        /// <summary>
        ///     Calculates the penetration depth of the axis-of-least-penetration for a
        /// </summary>
        /// <param name="target"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        float CalculatePenetration(IPhysBody target, IPhysBody source);
    }

    public struct DebugRayData
    {
        public DebugRayData(Ray ray, float maxLength, [CanBeNull] RayCastResults? results)
        {
            Ray = ray;
            MaxLength = maxLength;
            Results = results;
        }

        public Ray Ray
        {
            get;
        }

        public RayCastResults? Results { get; }
        public float MaxLength { get; }
    }
}

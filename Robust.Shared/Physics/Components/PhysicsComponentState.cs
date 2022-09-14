﻿using System;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Robust.Shared.Physics.Components
{
    [Serializable, NetSerializable]
    public sealed class PhysicsComponentState : ComponentState
    {
        public readonly bool CanCollide;
        public readonly bool SleepingAllowed;
        public readonly bool FixedRotation;
        public readonly BodyStatus Status;

        public readonly Vector2 LinearVelocity;
        public readonly float AngularVelocity;
        public readonly BodyType BodyType;

        /// <summary>
        ///
        /// </summary>
        /// <param name="canCollide"></param>
        /// <param name="sleepingAllowed"></param>
        /// <param name="fixedRotation"></param>
        /// <param name="status"></param>
        /// <param name="linearVelocity">Current linear velocity of the entity in meters per second.</param>
        /// <param name="angularVelocity">Current angular velocity of the entity in radians per sec.</param>
        /// <param name="bodyType"></param>
        public PhysicsComponentState(
            bool canCollide,
            bool sleepingAllowed,
            bool fixedRotation,
            BodyStatus status,
            Vector2 linearVelocity,
            float angularVelocity,
            BodyType bodyType)
        {
            CanCollide = canCollide;
            SleepingAllowed = sleepingAllowed;
            FixedRotation = fixedRotation;
            Status = status;

            LinearVelocity = linearVelocity;
            AngularVelocity = angularVelocity;
            BodyType = bodyType;
        }
    }
}

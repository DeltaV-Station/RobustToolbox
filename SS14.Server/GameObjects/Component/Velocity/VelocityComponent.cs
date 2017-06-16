﻿using SFML.System;
using SS14.Shared.GameObjects;
using SS14.Shared.GameObjects.Components.Velocity;
using SS14.Shared.IoC;

namespace SS14.Server.GameObjects
{
    public class VelocityComponent : Component
    {
        public override string Name => "Velocity";
        private Vector2f _velocity = new Vector2f();


        public VelocityComponent()
        {
            Family = ComponentFamily.Velocity;
        }

        public Vector2f Velocity
        {
            get { return _velocity; }
            set { _velocity = value; }
        }

        public float X
        {
            get { return Velocity.X; }
            set { Velocity = new Vector2f(value, Velocity.Y); }
        }

        public float Y
        {
            get { return Velocity.Y; }
            set { Velocity = new Vector2f(Velocity.X, value); }
        }

        public override void Shutdown()
        {
            Velocity = new Vector2f();
        }

        public override ComponentState GetComponentState()
        {
            return new VelocityComponentState(Velocity.X, Velocity.Y);
        }
    }
}

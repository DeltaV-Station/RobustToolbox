﻿using System;
using Robust.Shared.Interfaces.Serialization;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Shapes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Robust.Shared.Physics
{
    /// <summary>
    ///     A wrapper for shapes that's used to attach them to bodies with additional data such as mass or friction.
    /// </summary>
    public sealed class Fixture : IExposeData
    {
        // TODO: Need to call Dirty on shit I guess
        // TODO: All of this shit is stored in the broadphase directly

        /// <summary>
        ///     Proxies are essentially a wrapper around shape children.
        ///     Currently it's only used for chain shapes as all the other types have 1 child only.
        /// </summary>
        public FixtureProxy[] Proxies { get; private set; } = default!;

        // TODO: Just return Proxies.Count?
        public int ProxyCount { get; private set; }

        /// <summary>
        ///     Parent body of this fixture.
        /// </summary>
        public IPhysBody Body { get; private set; } = default!;

        /// <summary>
        ///     Our child shape.
        /// </summary>
        public Shape Shape { get; set; } = default!;

        /// <summary>
        ///     Are we hard-collidable or just used for collision events.
        /// </summary>
        public bool IsSensor
        {
            get => _isSensor;
            private set
            {
                if (_isSensor == value)
                    return;

                Body.Awake = true;
                _isSensor = value;
            }
        }

        private bool _isSensor;

        public float Friction { get; private set; }

        /// <summary>
        ///     How much bounce is there on collision.
        /// </summary>
        public float Restitution { get; set; }

        // TODO: Collision event handlers but we could probably use eventbus.

        /// <summary>
        ///     What layers do we collide with (for external entities).
        /// </summary>
        public int CollisionLayer
        {
            get => _collisionLayer;
            set
            {
                if (_collisionLayer == value)
                    return;

                _collisionLayer = value;
                Refilter();
            }
        }

        private int _collisionLayer;

        /// <summary>
        ///     What layers do we collide with that affects us.
        /// </summary>
        public int CollisionMask
        {
            get => _collisionMask;
            set
            {
                if (_collisionMask == value)
                    return;

                _collisionMask = value;
                Refilter();
            }
        }

        private int _collisionMask;

        public Fixture(Shape shape)
        {
            Shape = shape.Clone();

            Proxies = new FixtureProxy[Shape.ChildCount];
            ProxyCount = 0;
        }

        public void ExposeData(ObjectSerializer serializer)
        {
            serializer.DataReadWriteFunction("restitution", 0f, value => Restitution = value, () => Restitution);
            serializer.DataReadWriteFunction("friction", 0.2f, value => Friction = value, () => Friction);
        }

        private void Refilter()
        {
            var edge = Body.ContactList;
            while (edge != null)
            {
                var contact = edge.Contact;
                var fixtureA = contact.FixtureA;
                var fixtureB = contact.FixtureB;

                if (fixtureA == this || fixtureB == this)
                    contact.FilterFlag = true;

                edge = edge.Next;
            }

            // Touch each proxy to create new pairs
            var map = Body.PhysicsMap;

            if (map == null)
                return;

            var broadPhase = map.ContactManager.Broadphase;
            TouchProxies(broadPhase);
        }

        /// <summary>
        ///     Touch each proxy to create new pairs AKA did we move.
        /// </summary>
        /// <param name="broadPhase"></param>
        internal void TouchProxies(IBroadPhase broadPhase)
        {
            for (var i = 0; i < ProxyCount; i++)
                broadPhase.TouchProxies(Proxies[i].ProxyId);
        }

        /// <summary>
        ///     Test if a point is contained in this fixture.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool TestPoint(ref Vector2 point)
        {
            return Shape.TestPoint(Body.PhysicsTransform, ref point);
        }

        public bool RayCast(out RayCastOutput output, ref RayCastInput input, int childIndex)
        {
            return Shape.RayCast(out output, ref input, ref Body.Transform, childIndex);
        }

        /*
         * Okay TODO Sloth because you're falling asleep
         * Suss out AddProxy and Set Proxy and try to replace it with the C H U N K equivalent
         * The main thing im slowing down on is trying to add this shit to the broadphase but it looks like
         * Synchronize just uses the swept motion to update it.
         * Ideally:
         * For each fixture, get grids intersecting, then, work out grid-local positions for each and update it
         * ComputeAABB stuff should just work out our AABB relative to ourself I think...
         * Then when we move broadphase re-computes our grids intersecting and updates them all
         */

        /// <summary>
        ///     Add to broadphase
        /// </summary>
        internal void CreateProxies(IBroadPhase broadPhase, PhysicsTransform transform)
        {
            DebugTools.Assert(ProxyCount == 0);

            ProxyCount = Shape.ChildCount;

            for (var i = 0; i < ProxyCount; i++)
            {
                var proxy = new FixtureProxy
                {
                    Fixture = this,
                    ChildIndex = i,
                    AABB = Shape.ComputeAABB(transform, i),
                    ProxyId = broadPhase.AddProxy(ref proxy.AABB);
                };

                broadPhase.SetProxy(proxy.ProxyId, ref proxy);

                Proxies[i] = proxy;
            }
        }

        /// <summary>
        ///     Originally called "DestroyProxies" in aether2d
        /// </summary>
        /// <param name="broadPhase"></param>
        internal void RemoveFromBroadphase(IBroadPhase broadPhase)
        {
            // TODO
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Update this fixture in the broadphase
        /// </summary>
        /// <param name="broadPhase"></param>
        /// <param name="transform1"></param>
        /// <param name="transform2"></param>
        internal void Synchronize(IBroadPhase broadPhase, PhysicsTransform transform1, PhysicsTransform transform2)
        {
            // Fucking shitter

            // TODO: Okay so essentially every time we move we keep track of our swept AABB from our old position to our new position I think
            // Or something? Fucked if I know.
            // Okay looks like that may actually just be for DynamicTree purposes... I think? AHHHHHHHHHHH

            // Completely rewrote this given we don't need to fuck around with DynamicTree necessarily.
            // Also the lack of comments in the original is :))))

            // TODO: Get grid position
            // TODO: Update in broadphase
            throw new NotImplementedException();
        }

        /// <summary>
        ///     Clones the fixture onto the specified body.
        /// </summary>
        /// <param name="body">The body you wish to clone the fixture onto.</param>
        /// <returns>The cloned fixture.</returns>
        public Fixture CloneOnto(IPhysBody body)
        {
            return CloneOnto(body, Shape);
        }

        /// <summary>
        /// Clones the fixture and attached shape onto the specified body.
        /// Note: This is used only by Deserialization.
        /// </summary>
        /// <param name="body">The body you wish to clone the fixture onto.</param>
        /// <returns>The cloned fixture.</returns>
        internal Fixture CloneOnto(IPhysBody body, Shape shape)
        {
            Fixture fixture = new Fixture(shape.Clone())
            {
                Restitution = Restitution,
                Friction = Friction,
                IsSensor = IsSensor,
                CollisionLayer = CollisionLayer,
                CollisionMask = CollisionMask
            };

            body.Add(fixture);
            return fixture;
        }
    }
}

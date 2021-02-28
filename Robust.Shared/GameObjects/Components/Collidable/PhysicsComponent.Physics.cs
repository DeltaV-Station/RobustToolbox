﻿/*
* Farseer Physics Engine:
* Copyright (c) 2012 Ian Qvist
*
* Original source Box2D:
* Copyright (c) 2006-2011 Erin Catto http://www.box2d.org
*
* This software is provided 'as-is', without any express or implied
* warranty.  In no event will the authors be held liable for any damages
* arising from the use of this software.
* Permission is granted to anyone to use this software for any purpose,
* including commercial applications, and to alter it and redistribute it
* freely, subject to the following restrictions:
* 1. The origin of this software must not be misrepresented; you must not
* claim that you wrote the original software. If you use this software
* in a product, an acknowledgment in the product documentation would be
* appreciated but is not required.
* 2. Altered source versions must be plainly marked as such, and must not be
* misrepresented as being the original software.
* 3. This notice may not be removed or altered from any source distribution.
 *
 * PhysicsComponent is heavily modified from Box2D.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Containers;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Broadphase;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Dynamics.Contacts;
using Robust.Shared.Physics.Dynamics.Joints;
using Robust.Shared.Players;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Robust.Shared.GameObjects
{
    [ComponentReference(typeof(IPhysBody))]
    public sealed class PhysicsComponent : Component, IPhysBody
    {
        private BodyStatus _status;

        /// <inheritdoc />
        public override string Name => "Physics";

        /// <inheritdoc />
        public override uint? NetID => NetIDs.PHYSICS;

        /// <summary>
        ///     Has this body been added to an island previously in this tick.
        /// </summary>
        public bool Island { get; set; }

        /// <summary>
        ///     Store the body's index within the island so we can lookup its data.
        /// </summary>
        public int IslandIndex { get; set; }

        // TODO: Actually implement after the initial pr dummy
        /// <summary>
        ///     Gets or sets where this body should be included in the CCD solver.
        /// </summary>
        public bool IsBullet { get; set; }

        public bool IgnoreCCD { get; set; }

        /// <summary>
        ///     Linked-list of all of our contacts.
        /// </summary>
        internal ContactEdge? ContactEdges { get; set; } = null;

        /// <summary>
        ///     Linked-list of all of our joints.
        /// </summary>
        internal JointEdge? JointEdges { get; set; } = null;
        // TODO: Should there be a VV thing for joints? Would be useful. Same with contacts.
        // Though not sure how to do it well with the linked-list.

        public bool IgnorePaused { get; set; }
        public IEntity Entity => Owner;

        /// <inheritdoc />
        public MapId MapID => Owner.Transform.MapID;

        internal PhysicsMap PhysicsMap { get; set; } = default!;

        /// <inheritdoc />
        [ViewVariables(VVAccess.ReadWrite)]
        public BodyType BodyType
        {
            get => _bodyType;
            set
            {
                if (_bodyType == value)
                    return;

                _bodyType = value;

                ResetMassData();

                if (_bodyType == BodyType.Static)
                {
                    _linVelocity = Vector2.Zero;
                    _angVelocity = 0.0f;
                    // SynchronizeFixtures(); TODO: When CCD
                }

                Awake = true;

                Force = Vector2.Zero;
                Torque = 0.0f;

                RegenerateContacts();

                var oldAnchored = _bodyType == BodyType.Static;
                var anchored = _bodyType == BodyType.Static;

                if (oldAnchored != anchored)
                {
                    AnchoredChanged?.Invoke();
                    SendMessage(new AnchoredChangedMessage(Anchored));
                }
            }
        }

        private BodyType _bodyType;

        // We'll also block Static bodies from ever being awake given they don't need to move.
        /// <inheritdoc />
        [ViewVariables(VVAccess.ReadWrite)]
        public bool Awake
        {
            get => _awake;
            set
            {
                if (_awake == value)
                    return;

                _awake = value;

                if (value)
                {
                    _sleepTime = 0.0f;
                    PhysicsMap.ContactManager.UpdateContacts(ContactEdges, true);

                    Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new PhysicsWakeMessage(this));
                    SendMessage(new PhysicsWakeCompMessage(this));
                }
                else
                {
                    Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new PhysicsSleepMessage(this));
                    ResetDynamics();
                    _sleepTime = 0.0f;
                    PhysicsMap.ContactManager.UpdateContacts(ContactEdges, false);
                    SendMessage(new PhysicsSleepCompMessage(this));
                }

                Dirty();
            }
        }

        private bool _awake;

        /// <summary>
        /// You can disable sleeping on this body. If you disable sleeping, the
        /// body will be woken.
        /// </summary>
        /// <value><c>true</c> if sleeping is allowed; otherwise, <c>false</c>.</value>
        [ViewVariables(VVAccess.ReadWrite)]
        public bool SleepingAllowed
        {
            get => _sleepingAllowed;
            set
            {
                if (_sleepingAllowed == value)
                    return;

                if (!value)
                    Awake = true;

                _sleepingAllowed = value;
                Dirty();
            }
        }

        private bool _sleepingAllowed;

        [ViewVariables]
        public float SleepTime
        {
            get => _sleepTime;
            set
            {
                DebugTools.Assert(!float.IsNaN(value));

                if (MathHelper.CloseTo(value, _sleepTime))
                    return;

                _sleepTime = value;
            }
        }

        private float _sleepTime;

        /// <inheritdoc />
        public void WakeBody()
        {
            Awake = true;
        }

        /// <summary>
        ///     Removes all of our contacts and flags them as requiring regeneration next physics tick.
        /// </summary>
        public void RegenerateContacts()
        {
            var contactEdge = ContactEdges;
            while (contactEdge != null)
            {
                var contactEdge0 = contactEdge;
                contactEdge = contactEdge.Next;
                PhysicsMap.ContactManager.Destroy(contactEdge0.Contact!);
            }

            ContactEdges = null;
            var broadphaseSystem = EntitySystem.Get<SharedBroadPhaseSystem>();

            foreach (var fixture in Fixtures)
            {
                var proxyCount = fixture.ProxyCount;
                foreach (var (gridId, proxies) in fixture.Proxies)
                {
                    var broadPhase = broadphaseSystem.GetBroadPhase(Owner.Transform.MapID, gridId);
                    if (broadPhase == null) continue;
                    for (var i = 0; i < proxyCount; i++)
                    {
                        broadPhase.TouchProxy(proxies[i].ProxyId);
                    }
                }
            }
        }

        /// <inheritdoc />
        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(ref _canCollide, "on", true);
            serializer.DataField(ref _status, "status", BodyStatus.OnGround);
            // Farseer defaults this to static buuut knowing our audience most are gonnna forget to set it.
            serializer.DataField(ref _bodyType, "bodyType", BodyType.Dynamic);
            serializer.DataField(ref _fixedRotation, "fixedRotation", true);
            serializer.DataReadWriteFunction("fixtures", new List<Fixture>(), fixtures =>
            {
                foreach (var fixture in fixtures)
                {
                    fixture.Body = this;
                    _fixtures.Add(fixture);
                }
            }, () => Fixtures);

            serializer.DataReadWriteFunction("joints", new List<Joint>(), joints =>
            {
                if (joints.Count == 0) return;

                // TODO: Brain no worky rn
                throw new NotImplementedException();
            }, () =>
            {
                var joints = new List<Joint>();

                for (var jn = JointEdges; jn != null; jn = jn.Next)
                {
                    joints.Add(jn.Joint);
                }

                return joints;
            });

            // TODO: Dump someday
            serializer.DataReadFunction("anchored", true, value =>
            {
                _bodyType = value ? BodyType.Static : BodyType.Dynamic;
            });

            serializer.DataField(ref _linearDamping, "linearDamping", 0.02f);
            serializer.DataField(ref _angularDamping, "angularDamping", 0.02f);
            serializer.DataField(ref _mass, "mass", 1.0f);
            if (_mass > 0f && BodyType == BodyType.Dynamic)
            {
                _invMass = 1.0f / _mass;
            }
            serializer.DataField(ref _awake, "awake", true);
            serializer.DataField(ref _sleepingAllowed, "sleepingAllowed", true);
        }

        /// <inheritdoc />
        public override ComponentState GetComponentState(ICommonSession session)
        {
            // TODO: Could optimise the shit out of this because only linear velocity and angular velocity are changing 99% of the time.
            var joints = new List<Joint>();
            for (var je = JointEdges; je != null; je = je.Next)
            {
                joints.Add(je.Joint);
            }

            return new PhysicsComponentState(_canCollide, _sleepingAllowed, _fixedRotation, _status, _fixtures, joints, _mass, LinearVelocity, AngularVelocity, BodyType);
        }

        /// <inheritdoc />
        public override void HandleComponentState(ComponentState? curState, ComponentState? nextState)
        {
            if (curState is not PhysicsComponentState newState)
                return;

            SleepingAllowed = newState.SleepingAllowed;
            FixedRotation = newState.FixedRotation;
            CanCollide = newState.CanCollide;
            Status = newState.Status;

            // So transform doesn't apply MapId in the HandleComponentState because ??? so MapId can still be 0.
            // Fucking kill me, please. You have no idea deep the rabbit hole of shitcode goes to make this work.
            // PJB, please forgive me and come up with something better.

            // We will pray that this deferred joint is handled properly.

            // TODO: Crude as FUCK diffing here as well, fine for now.
            /*
             * -- Joints --
             */

            var existingJoints = new List<Joint>();

            for (var je = JointEdges; je != null; je = je.Next)
            {
                existingJoints.Add(je.Joint);
            }

            var jointsDiff = true;

            if (existingJoints.Count == newState.Joints.Count)
            {
                var anyDiff = false;
                for (var i = 0; i < existingJoints.Count; i++)
                {
                    var existing = existingJoints[i];
                    var newJoint = newState.Joints[i];

                    if (!existing.Equals(newJoint))
                    {
                        anyDiff = true;
                        break;
                    }
                }

                if (!anyDiff)
                {
                    jointsDiff = false;
                }
            }

            if (jointsDiff)
            {
                ClearJoints();

                foreach (var joint in newState.Joints)
                {
                    joint.EdgeA = new JointEdge();
                    joint.EdgeB = new JointEdge();
                    // Defer joints given it relies on 2 bodies.
                    AddJoint(joint);
                }
            }

            /*
             * -- Fixtures --
             */

            var toAdd = new List<Fixture>();
            var toRemove = new List<Fixture>();

            // TODO: This diffing is crude (muh ordering) but at least it will save the broadphase updates 90% of the time.
            for (var i = 0; i < newState.Fixtures.Count; i++)
            {
                var newFixture = new Fixture();
                newState.Fixtures[i].CopyTo(newFixture);

                newFixture.Body = this;

                // Existing fixture
                if (_fixtures.Count > i)
                {
                    var existingFixture = _fixtures[i];

                    if (!existingFixture.Equals(newFixture))
                    {
                        toRemove.Add(existingFixture);
                        toAdd.Add(newFixture);
                    }
                }
                else
                {
                    toAdd.Add(newFixture);
                }
            }

            foreach (var fixture in toRemove)
            {
                RemoveFixture(fixture);
            }

            foreach (var fixture in toAdd)
            {
                AddFixture(fixture);
                fixture.Shape.ApplyState();
            }

            /*
             * -- Sundries --
             */

            Dirty();
            // TODO: Should transform just be doing this??? UpdateEntityTree();
            Mass = newState.Mass / 1000f; // gram to kilogram

            LinearVelocity = newState.LinearVelocity;
            // Logger.Debug($"{IGameTiming.TickStampStatic}: [{Owner}] {LinearVelocity}");
            AngularVelocity = newState.AngularVelocity;
            BodyType = newState.BodyType;
            Predict = false;
        }

        /// <summary>
        /// Resets the dynamics of this body.
        /// Sets torque, force and linear/angular velocity to 0
        /// </summary>
        public void ResetDynamics()
        {
            Torque = 0;
            _angVelocity = 0;
            Force = Vector2.Zero;
            _linVelocity = Vector2.Zero;
            Dirty();
        }

        public Box2 GetWorldAABB(IMapManager? mapManager)
        {
            mapManager ??= IoCManager.Resolve<IMapManager>();
            var bounds = new Box2();

            foreach (var fixture in _fixtures)
            {
                foreach (var (gridId, proxies) in fixture.Proxies)
                {
                    Vector2 offset;

                    if (gridId == GridId.Invalid)
                    {
                        offset = Vector2.Zero;
                    }
                    else
                    {
                        offset = mapManager.GetGrid(gridId).WorldPosition;
                    }

                    foreach (var proxy in proxies)
                    {
                        var shapeBounds = proxy.AABB.Translated(offset);
                        bounds = bounds.IsEmpty() ? shapeBounds : bounds.Union(shapeBounds);
                    }
                }
            }

            return bounds.IsEmpty() ? Box2.UnitCentered.Translated(Owner.Transform.WorldPosition) : bounds;
        }

        /// <inheritdoc />
        [ViewVariables]
        public IReadOnlyList<Fixture> Fixtures => _fixtures;

        private List<Fixture> _fixtures = new();

        /// <summary>
        ///     Enables or disabled collision processing of this component.
        /// </summary>
        /// <remarks>
        ///     Also known as Enabled in Box2D
        /// </remarks>
        [ViewVariables(VVAccess.ReadWrite)]
        public bool CanCollide
        {
            get => _canCollide;
            set
            {
                if (_canCollide == value)
                    return;

                _canCollide = value;

                Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new CollisionChangeMessage(this, Owner.Uid, _canCollide));
                Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new PhysicsUpdateMessage(this));
                Dirty();
            }
        }

        private bool _canCollide;

        /// <summary>
        ///     Non-hard physics bodies will not cause action collision (e.g. blocking of movement)
        ///     while still raising collision events. Recommended you use the fixture hard values directly
        /// </summary>
        /// <remarks>
        ///     This is useful for triggers or such to detect collision without actually causing a blockage.
        /// </remarks>
        [ViewVariables(VVAccess.ReadWrite)]
        public bool Hard
        {
            get
            {
                foreach (var fixture in Fixtures)
                {
                    if (fixture.Hard) return true;
                }

                return false;
            }
            set
            {
                foreach (var fixture in Fixtures)
                {
                    fixture.Hard = value;
                }
            }
        }

        /// <summary>
        ///     Bitmask of the collision layers this component is a part of.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public int CollisionLayer
        {
            get
            {
                var layers = 0x0;

                foreach (var fixture in Fixtures)
                    layers |= fixture.CollisionLayer;
                return layers;
            }
        }

        /// <summary>
        ///     Bitmask of the layers this component collides with.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public int CollisionMask
        {
            get
            {
                var mask = 0x0;

                foreach (var fixture in Fixtures)
                    mask |= fixture.CollisionMask;
                return mask;
            }
        }

        [ViewVariables]
        public bool HasProxies { get; set; }

        /// <summary>
        ///     Current mass of the entity in kilograms.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float Mass
        {
            get => BodyType == BodyType.Dynamic ? _mass : 0.0f;
            set
            {
                DebugTools.Assert(!float.IsNaN(value));

                if (MathHelper.CloseTo(_mass, value))
                    return;

                // Box2D blocks it if it's dynamic but in case objects can flip-flop between dynamic and static easily via anchoring.
                // So we may as well support it and just guard the InvMass get
                _mass = value;

                if (_mass <= 0.0f)
                    _mass = 1.0f;

                _invMass = 1.0f / _mass;
                Dirty();
            }
        }

        private float _mass;

        /// <summary>
        ///     Inverse mass of the entity in kilograms (1 / Mass).
        /// </summary>
        public float InvMass => BodyType == BodyType.Dynamic ? _invMass : 0.0f;

        private float _invMass;

        /// <summary>
        /// Moment of inertia, or angular mass, in kg * m^2.
        /// </summary>
        /// <remarks>
        /// https://en.wikipedia.org/wiki/Moment_of_inertia
        /// </remarks>
        [ViewVariables(VVAccess.ReadWrite)]
        public float Inertia
        {
            get => _inertia + Mass * Vector2.Dot(Vector2.Zero, Vector2.Zero); // TODO: Sweep.LocalCenter
            set
            {
                DebugTools.Assert(!float.IsNaN(value));

                if (_bodyType != BodyType.Dynamic) return;

                if (MathHelper.CloseTo(_inertia, value)) return;

                if (value > 0.0f && !_fixedRotation)
                {
                    _inertia = value - Mass * Vector2.Dot(LocalCenter, LocalCenter);
                    DebugTools.Assert(_inertia > 0.0f);
                    InvI = 1.0f / _inertia;
                    Dirty();
                }
            }
        }

        private float _inertia;

        /// <summary>
        ///     Indicates whether this body ignores gravity
        /// </summary>
        public bool IgnoreGravity { get; set; }

        /// <summary>
        /// Inverse moment of inertia (1 / I).
        /// </summary>
        public float InvI { get; set; }

        /// <summary>
        ///     Is the body allowed to have angular velocity.
        /// </summary>
        public bool FixedRotation
        {
            get => _fixedRotation;
            set
            {
                if (_fixedRotation == value)
                    return;

                _fixedRotation = value;
                _angVelocity = 0.0f;
                ResetMassData();
                Dirty();
            }
        }

        private bool _fixedRotation;

        // TODO: Will be used someday
        /// <summary>
        ///     Get this body's center of mass offset to world position.
        /// </summary>
        /// <remarks>
        ///     AKA Sweep.LocalCenter in Box2D.
        ///     Not currently in use as this is set after mass data gets set (when fixtures update).
        /// </remarks>
        public Vector2 LocalCenter
        {
            get => _localCenter;
            set
            {
                if (_bodyType != BodyType.Dynamic) return;
                if (value.EqualsApprox(_localCenter)) return;

                throw new NotImplementedException();
            }
        }

        private Vector2 _localCenter;

        /// <summary>
        /// Current Force being applied to this entity in Newtons.
        /// </summary>
        /// <remarks>
        /// The force is applied to the center of mass.
        /// https://en.wikipedia.org/wiki/Force
        /// </remarks>
        [ViewVariables(VVAccess.ReadWrite)]
        public Vector2 Force { get; set; }

        /// <summary>
        /// Current torque being applied to this entity in N*m.
        /// </summary>
        /// <remarks>
        /// The torque rotates around the Z axis on the object.
        /// https://en.wikipedia.org/wiki/Torque
        /// </remarks>
        [ViewVariables(VVAccess.ReadWrite)]
        public float Torque { get; set; }

        /// <summary>
        ///     Contact friction between 2 bodies.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float Friction
        {
            get => _friction;
            set
            {
                if (MathHelper.CloseTo(value, _friction))
                    return;

                _friction = value;
                // TODO
                // Dirty();
            }
        }

        private float _friction;

        /// <summary>
        ///     This is a set amount that the body's linear velocity is reduced by every tick.
        ///     Combined with the tile friction.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float LinearDamping
        {
            get => _linearDamping;
            set
            {
                DebugTools.Assert(!float.IsNaN(value));

                if (MathHelper.CloseTo(value, _linearDamping))
                    return;

                _linearDamping = value;
                // Dirty();
            }
        }

        private float _linearDamping;

        /// <summary>
        ///     This is a set amount that the body's angular velocity is reduced every tick.
        ///     Combined with the tile friction.
        /// </summary>
        /// <returns></returns>
        [ViewVariables(VVAccess.ReadWrite)]
        public float AngularDamping
        {
            get => _angularDamping;
            set
            {
                DebugTools.Assert(!float.IsNaN(value));

                if (MathHelper.CloseTo(value, _angularDamping))
                    return;

                _angularDamping = value;
                // Dirty();
            }
        }

        private float _angularDamping;

        /// <summary>
        ///     Current linear velocity of the entity in meters per second.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public Vector2 LinearVelocity
        {
            get => _linVelocity;
            set
            {
                DebugTools.Assert(!float.IsNaN(value.X) && !float.IsNaN(value.Y));

                if (BodyType == BodyType.Static)
                    return;

                if (Vector2.Dot(value, value) > 0.0f)
                    Awake = true;

                if (_linVelocity.EqualsApprox(value, 0.0001))
                    return;

                _linVelocity = value;
                Dirty();
            }
        }

        private Vector2 _linVelocity;

        /// <summary>
        ///     Current angular velocity of the entity in radians per sec.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public float AngularVelocity
        {
            get => _angVelocity;
            set
            {
                DebugTools.Assert(!float.IsNaN(value));

                if (BodyType == BodyType.Static)
                    return;

                if (value * value > 0.0f)
                    Awake = true;

                if (MathHelper.CloseTo(_angVelocity, value))
                    return;

                _angVelocity = value;
                Dirty();
            }
        }

        private float _angVelocity;

        /// <summary>
        ///     Current momentum of the entity in kilogram meters per second
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public Vector2 Momentum
        {
            get => LinearVelocity * Mass;
            set => LinearVelocity = value / Mass;
        }

        /// <summary>
        ///     The current status of the object
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public BodyStatus Status
        {
            get => _status;
            set
            {
                if (_status == value)
                    return;

                _status = value;
                Dirty();
            }
        }

        /// <summary>
        ///     Whether this component is on the ground
        /// </summary>
        public bool OnGround => Status == BodyStatus.OnGround &&
                                !IoCManager.Resolve<IPhysicsManager>()
                                    .IsWeightless(Owner.Transform.Coordinates);

        /// <summary>
        ///     Whether or not the entity is anchored in place.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        [Obsolete("Use BodyType.Static instead")]
        public bool Anchored
        {
            get => BodyType == BodyType.Static;
            set
            {
                var anchored = BodyType == BodyType.Static;

                if (anchored == value)
                    return;

                if (value)
                {
                    _bodyType = BodyType.Static;
                }
                else
                {
                    _bodyType = BodyType.Dynamic;
                }

                AnchoredChanged?.Invoke();
                SendMessage(new AnchoredChangedMessage(Anchored));
                Dirty();
            }
        }

        [Obsolete("Use AnchoredChangedMessage instead")]
        public event Action? AnchoredChanged;

        [ViewVariables(VVAccess.ReadWrite)]
        public bool Predict
        {
            get => _predict;
            set => _predict = value;
        }

        private bool _predict;

        /// <summary>
        ///     As we defer updates need to store the MapId we used for broadphase.
        /// </summary>
        public MapId BroadphaseMapId { get; set; }

        public IEnumerable<PhysicsComponent> GetCollidingBodies()
        {
            foreach (var entity in EntitySystem.Get<SharedBroadPhaseSystem>().GetCollidingEntities(this, Vector2.Zero))
            {
                yield return entity;
            }
        }

        public IEnumerable<PhysicsComponent> GetBodiesIntersecting()
        {
            foreach (var entity in EntitySystem.Get<SharedBroadPhaseSystem>().GetCollidingEntities(Owner.Transform.MapID, GetWorldAABB()))
            {
                yield return entity;
            }
        }

        /// <summary>
        /// Gets a local point relative to the body's origin given a world point.
        /// Note that the vector only takes the rotation into account, not the position.
        /// </summary>
        /// <param name="worldPoint">A point in world coordinates.</param>
        /// <returns>The corresponding local point relative to the body's origin.</returns>
        public Vector2 GetLocalPoint(in Vector2 worldPoint)
        {
            return Physics.Transform.MulT(GetTransform(), worldPoint);
        }

        /// <summary>
        /// Get the world coordinates of a point given the local coordinates.
        /// </summary>
        /// <param name="localPoint">A point on the body measured relative the the body's origin.</param>
        /// <returns>The same point expressed in world coordinates.</returns>
        public Vector2 GetWorldPoint(in Vector2 localPoint)
        {
            return Transform.Mul(GetTransform(), localPoint);
        }

        /// <summary>
        ///     Remove the proxies from all the broadphases.
        /// </summary>
        public void ClearProxies()
        {
            if (!HasProxies) return;

            var broadPhaseSystem = EntitySystem.Get<SharedBroadPhaseSystem>();
            var mapId = BroadphaseMapId;

            foreach (var fixture in Fixtures)
            {
                fixture.ClearProxies(mapId, broadPhaseSystem);
            }

            HasProxies = false;
        }

        public void FixtureChanged(Fixture fixture)
        {
            // TODO: Optimise this a LOT
            Dirty();
            Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new FixtureUpdateMessage(this, fixture));
        }

        internal Transform GetTransform()
        {
            return new(Owner.Transform.WorldPosition, (float) Owner.Transform.WorldRotation.Theta);
        }

        public void ApplyLinearImpulse(in Vector2 impulse)
        {
            if (_bodyType != BodyType.Dynamic) return;
            Awake = true;

            LinearVelocity += impulse * _invMass;
        }

        public void ApplyAngularImpulse(float impulse)
        {
            if (_bodyType != BodyType.Dynamic) return;
            Awake = true;

            AngularVelocity += impulse * InvI;
        }

        public void ApplyForce(in Vector2 force)
        {
            if (_bodyType != BodyType.Dynamic) return;

            Awake = true;
            Force += force;
        }

        /// <summary>
        ///     Calculate our AABB without using proxies.
        /// </summary>
        /// <returns></returns>
        public Box2 GetWorldAABB()
        {
            var mapId = Owner.Transform.MapID;
            if (mapId == MapId.Nullspace)
                return new Box2();

            var worldRotation = Owner.Transform.WorldRotation;
            var bounds = new Box2();

            foreach (var fixture in Fixtures)
            {
                var aabb = fixture.Shape.CalculateLocalBounds(worldRotation);
                bounds = bounds.Union(aabb);
            }

            return bounds.Translated(Owner.Transform.WorldPosition);
        }

        /// <summary>
        ///     Get the proxies for each of our fixtures and add them to the broadphases.
        /// </summary>
        /// <param name="mapManager"></param>
        /// <param name="broadPhaseSystem"></param>
        public void CreateProxies(IMapManager? mapManager = null, SharedBroadPhaseSystem? broadPhaseSystem = null)
        {
            if (HasProxies) return;

            BroadphaseMapId = Owner.Transform.MapID;

            if (BroadphaseMapId == MapId.Nullspace)
            {
                HasProxies = true;
                return;
            }

            broadPhaseSystem ??= EntitySystem.Get<SharedBroadPhaseSystem>();
            mapManager ??= IoCManager.Resolve<IMapManager>();
            var worldPosition = Owner.Transform.WorldPosition;
            var mapId = Owner.Transform.MapID;
            var worldAABB = GetWorldAABB();
            var worldRotation = Owner.Transform.WorldRotation.Theta;

            // TODO: For singularity and shuttles: Any fixtures that have a MapGrid layer / mask needs to be added to the default broadphase (so it can collide with grids).

            foreach (var gridId in mapManager.FindGridIdsIntersecting(mapId, worldAABB, true))
            {
                var broadPhase = broadPhaseSystem.GetBroadPhase(mapId, gridId);
                DebugTools.AssertNotNull(broadPhase);
                if (broadPhase == null) continue; // TODO

                Vector2 offset = worldPosition;
                double gridRotation = worldRotation;

                if (gridId != GridId.Invalid)
                {
                    var grid = mapManager.GetGrid(gridId);
                    offset -= grid.WorldPosition;
                    // TODO: Should probably have a helper for this
                    gridRotation = worldRotation - Owner.EntityManager.GetEntity(grid.GridEntityId).Transform.WorldRotation;
                }

                foreach (var fixture in Fixtures)
                {
                    fixture.ProxyCount = fixture.Shape.ChildCount;
                    var proxies = new FixtureProxy[fixture.ProxyCount];

                    // TODO: Will need to pass in childIndex to this as well
                    for (var i = 0; i < fixture.ProxyCount; i++)
                    {
                        var aabb = fixture.Shape.CalculateLocalBounds(gridRotation).Translated(offset);

                        var proxy = new FixtureProxy(aabb, fixture, i);

                        proxy.ProxyId = broadPhase.AddProxy(ref proxy);
                        proxies[i] = proxy;
                        DebugTools.Assert(proxies[i].ProxyId != DynamicTree.Proxy.Free);
                    }

                    fixture.SetProxies(gridId, proxies);
                }
            }

            HasProxies = true;
        }

        public void DestroyContacts()
        {
            ContactEdge? contactEdge = ContactEdges;
            while (contactEdge != null)
            {
                var contactEdge0 = contactEdge;
                contactEdge = contactEdge.Next;
                PhysicsMap.ContactManager.Destroy(contactEdge0.Contact!);
            }

            ContactEdges = null;
        }

        IEnumerable<IPhysBody> IPhysBody.GetCollidingEntities(Vector2 offset, bool approx)
        {
            return EntitySystem.Get<SharedBroadPhaseSystem>().GetCollidingEntities(this, offset, approx);
        }

        /// <inheritdoc />
        public bool CanMove()
        {
            return BodyType == BodyType.Dynamic || (!Anchored && Mass > 0);
        }

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            if (BodyType == BodyType.Static)
            {
                _awake = false;
            }

            Dirty();
            // Yeah yeah TODO Combine these
            // Implicitly assume that stuff doesn't cover if a non-collidable is initialized.

            if (CanCollide)
            {
                if (!Awake)
                {
                    Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new PhysicsSleepMessage(this));
                    SendMessage(new PhysicsSleepCompMessage(this));
                }
                else
                {
                    Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new PhysicsWakeMessage(this));
                    SendMessage(new PhysicsWakeCompMessage(this));
                }

                if (Owner.IsInContainer())
                {
                    _canCollide = false;
                }
                else
                {
                    // TODO: Probably a bad idea but ehh future sloth's problem; namely that we have to duplicate code between here and CanCollide.
                    Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new CollisionChangeMessage(this, Owner.Uid, _canCollide));
                    Owner.EntityManager.EventBus.RaiseEvent(EventSource.Local, new PhysicsUpdateMessage(this));
                }
            }

            if (EntitySystem.Get<SharedPhysicsSystem>().Maps.TryGetValue(Owner.Transform.MapID, out var map))
            {
                PhysicsMap = map;
            }
        }

        public void ClearJoints()
        {
            for (var je = JointEdges; je != null; je = je.Next)
            {
                RemoveJoint(je.Joint);
            }
        }

        public void AddFixture(Fixture fixture)
        {
            // TODO: SynchronizeFixtures could be more optimally done. Maybe just eventbus it
            // Also we need to queue updates and also not teardown completely every time.
            _fixtures.Add(fixture);
            Dirty();
            EntitySystem.Get<SharedBroadPhaseSystem>().AddFixture(this, fixture);
        }

        public void RemoveFixture(Fixture fixture)
        {
            if (!_fixtures.Contains(fixture))
            {
                Logger.ErrorS("physics", $"Tried to remove fixture that isn't attached to the body {Owner.Uid}");
                return;
            }

            ContactEdge? edge = ContactEdges;
            while (edge != null)
            {
                var contact = edge.Contact!;
                edge = edge.Next;

                var fixtureA = contact.FixtureA;
                var fixtureB = contact.FixtureB;

                if (fixture == fixtureA || fixture == fixtureB)
                {
                    PhysicsMap.ContactManager.Destroy(contact);
                }
            }

            _fixtures.Remove(fixture);
            EntitySystem.Get<SharedBroadPhaseSystem>().RemoveFixture(this, fixture);
            ResetMassData();

            Dirty();
        }

        public void AddJoint(Joint joint)
        {
            PhysicsMap.AddJoint(joint);
        }

        public void RemoveJoint(Joint joint)
        {
            PhysicsMap.RemoveJoint(joint);
        }

        public override void OnRemove()
        {
            base.OnRemove();

            CanCollide = false;
        }

        public void ResetMassData()
        {
            // _mass = 0.0f;
            // _invMass = 0.0f;
            _inertia = 0.0f;
            InvI = 0.0f;
            // Sweep

            if (BodyType == BodyType.Kinematic)
            {
                return;
            }

            DebugTools.Assert(BodyType == BodyType.Dynamic || BodyType == BodyType.Static);

            var localCenter = Vector2.Zero;

            foreach (var fixture in Fixtures)
            {
                // TODO: Density
                continue;
                // if (fixture.Shape.Density == 0.0f)
            }

            if (BodyType == BodyType.Static)
            {
                return;
            }

            if (_mass > 0.0f)
            {
                _invMass = 1.0f / _mass;
                localCenter *= _invMass;
            }
            else
            {
                // Always need positive mass.
                _mass = 1.0f;
                _invMass = 1.0f;
            }

            if (_inertia > 0.0f && !_fixedRotation)
            {
                // Center inertia about center of mass.
                _inertia -= _mass * Vector2.Dot(localCenter, localCenter);

                DebugTools.Assert(_inertia > 0.0f);
                InvI = 1.0f / _inertia;
            }
            else
            {
                _inertia = 0.0f;
                InvI = 0.0f;
            }

            /* TODO
            // Move center of mass;
            var oldCenter = _sweep.Center;
            _sweep.LocalCenter = localCenter;
            _sweep.Center0 = _sweep.Center = Physics.Transform.Mul(GetTransform(), _sweep.LocalCenter);

            // Update center of mass velocity.
            var a = _sweep.Center - oldCenter;
            _linVelocity += new Vector2(-_angVelocity * a.y, _angVelocity * a.x);
            */
        }

        /// <summary>
        ///     Used to prevent bodies from colliding; may lie depending on joints.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        internal bool ShouldCollide(PhysicsComponent other)
        {
            // At least one body should be dynamic.
            if (_bodyType != BodyType.Dynamic && other._bodyType != BodyType.Dynamic)
            {
                return false;
            }

            // Does a joint prevent collision?
            for (var jn = JointEdges; jn != null; jn = jn.Next)
            {
                if (jn.Other != other) continue;
                if (jn.Joint.CollideConnected) continue;
                return false;
            }

            foreach (var comp in Owner.GetAllComponents<ICollideSpecial>())
            {
                if (comp.PreventCollide(other)) return false;
            }

            foreach (var comp in other.Owner.GetAllComponents<ICollideSpecial>())
            {
                if (comp.PreventCollide(this)) return false;
            }

            return true;
        }

        public bool IsOnGround()
        {
            return Status == BodyStatus.OnGround;
        }

        public bool IsInAir()
        {
            return Status == BodyStatus.InAir;
        }
    }

    public class AnchoredChangedMessage : ComponentMessage
    {
        public readonly bool Anchored;

        public AnchoredChangedMessage(bool anchored)
        {
            Anchored = anchored;
        }
    }
}

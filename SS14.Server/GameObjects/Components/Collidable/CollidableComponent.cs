﻿using System.Collections.Generic;
using System.Linq;
using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.Interfaces.GameObjects.Components;
using SS14.Shared.Interfaces.Network;
using SS14.Shared.Interfaces.Physics;
using SS14.Shared.IoC;
using SS14.Shared.Map;
using SS14.Shared.Maths;
using SS14.Shared.Serialization;
using SS14.Shared.ViewVariables;

namespace SS14.Server.GameObjects
{
    public class CollidableComponent : Component, ICollidableComponent
    {
        [Dependency] private readonly IPhysicsManager _physicsManager;

        private bool _collisionEnabled;
        private bool _isHardCollidable;
        private int _collisionLayer; //bitfield
        private int _collisionMask; //bitfield

        /// <inheritdoc />
        public override string Name => "Collidable";

        /// <inheritdoc />
        public override uint? NetID => NetIDs.COLLIDABLE;

        /// <inheritdoc />
        public MapId MapID => Owner.Transform.MapID;

        /// <inheritdoc />
        public override void ExposeData(ObjectSerializer serializer)
        {
            base.ExposeData(serializer);

            serializer.DataField(ref _collisionEnabled, "on", true);
            serializer.DataField(ref _isHardCollidable, "hard", true);
            serializer.DataField(ref _collisionLayer, "layer", 0x1);
            serializer.DataField(ref _collisionMask, "mask", 0x1);
            serializer.DataField(ref _isInteractingWithFloor, "IsInteractingWithFloor", false);
        }

        /// <inheritdoc />
        public override ComponentState GetComponentState()
        {
            return new CollidableComponentState(_collisionEnabled, _isHardCollidable, _collisionLayer, _collisionMask);
        }

        /// <inheritdoc />
        Box2 ICollidable.WorldAABB => Owner.GetComponent<BoundingBoxComponent>().WorldAABB;

        /// <inheritdoc />
        Box2 ICollidable.AABB => Owner.GetComponent<BoundingBoxComponent>().AABB;

        /// <summary>
        ///     Enables or disabled collision processing of this component.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public bool CollisionEnabled
        {
            get => _collisionEnabled;
            set => _collisionEnabled = value;
        }

        /// <inheritdoc />
        [ViewVariables(VVAccess.ReadWrite)]
        public bool IsHardCollidable
        {
            get => _isHardCollidable;
            set => _isHardCollidable = value;
        }

        /// <summary>
        ///     Bitmask of the collision layers this component is a part of.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public int CollisionLayer
        {
            get => _collisionLayer;
            set => _collisionLayer = value;
        }

        /// <summary>
        ///     Bitmask of the layers this component collides with.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public int CollisionMask
        {
            get => _collisionMask;
            set => _collisionMask = value;
        }

        private bool _isInteractingWithFloor;
        /// <summary>
        ///     When this enity moves it is actively scraping against the floor tile it is on.
        /// </summary>
        [ViewVariables(VVAccess.ReadWrite)]
        public bool IsInteractingWithFloor
        {
            get => _isInteractingWithFloor;
            set => _isInteractingWithFloor = value;
        }

        /// <inheritdoc />
        void ICollidable.Bumped(IEntity bumpedby)
        {
            SendMessage(new BumpedEntMsg(bumpedby));
        }

        /// <inheritdoc />
        void ICollidable.Bump(List<IEntity> bumpedinto)
        {
            var collidecomponents = Owner.GetAllComponents<ICollideBehavior>().ToList();

            for (var i = 0; i < collidecomponents.Count; i++)
            {
                collidecomponents[i].CollideWith(bumpedinto);
            }
        }

        /// <inheritdoc />
        public override void Startup()
        {
            base.Startup();

            _physicsManager.AddCollidable(this);
        }

        /// <inheritdoc />
        public override void Shutdown()
        {
            _physicsManager.RemoveCollidable(this);

            base.Shutdown();
        }

        /// <inheritdoc />
        public bool TryCollision(Vector2 offset, bool bump = false)
        {
            if (!_collisionEnabled || CollisionMask == 0x0)
                return false;

            return _physicsManager.TryCollide(Owner, offset, bump);
        }
    }
}

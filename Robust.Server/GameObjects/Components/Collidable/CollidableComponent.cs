﻿using System.Collections.Generic;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.GameObjects.Components;
using Robust.Shared.Interfaces.Physics;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Robust.Server.GameObjects
{
    public class CollidableComponent : Component, ICollidableComponent
    {
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

            var cm = IoCManager.Resolve<IPhysicsManager>();
            cm.AddCollidable(this);
        }

        /// <inheritdoc />
        public override void Shutdown()
        {
            var cm = IoCManager.Resolve<IPhysicsManager>();
            cm.RemoveCollidable(this);

            base.Shutdown();
        }

        /// <inheritdoc />
        public bool TryCollision(Vector2 offset, bool bump = false)
        {
            if (!_collisionEnabled || CollisionMask == 0x0)
                return false;

            return IoCManager.Resolve<IPhysicsManager>().TryCollide(Owner, offset, bump);
        }
    }
}

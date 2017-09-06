﻿using System;
using OpenTK;
using SS14.Shared;
using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.Interfaces.GameObjects.Components;
using SS14.Shared.Maths;
using SS14.Server.Interfaces.GameObjects;
using SS14.Shared.Map;
using SS14.Shared.IoC;
using SS14.Shared.Interfaces.Map;

namespace SS14.Server.GameObjects
{
    /// <summary>
    ///     Stores the position and orientation of the entity.
    /// </summary>
    public class TransformComponent : Component, IServerTransformComponent
    {
        /// <summary>
        ///     Current parent entity of this entity.
        /// </summary>
        public ITransformComponent Parent { get; set; }

        private Vector2 _position;
        private int MapID;
        private int GridID;

        /// <summary>
        ///     Current rotation offset of the entity.
        /// </summary>
        public Angle Rotation { get; set; }

        /// <inheritdoc />
        public override string Name => "Transform";

        /// <inheritdoc />
        public override uint? NetID => NetIDs.TRANSFORM;

        /// <inheritdoc />
        public event EventHandler<VectorEventArgs> OnMove;

        /// <inheritdoc />
        public LocalCoordinates Position
        {
            get
            {
                if (Parent != null)
                {
                    return GetMapTransform().Position; //Search up the tree for the true map position
                }
                else
                {
                    return new LocalCoordinates(_position, GridID, MapID);
                }
            }
            set
            {
                var oldPosition = _position;
                _position = value.Position;

                var invokeonmove = (MapID == value.MapID && GridID == value.GridID);
                MapID = value.MapID;
                GridID = value.GridID;

                if(invokeonmove)
                    OnMove?.Invoke(this, new VectorEventArgs(oldPosition, _position));
            }
        }

        public Vector2 WorldPosition
        {
            get
            {
                if (Parent != null)
                {
                    return GetMapTransform().WorldPosition; //Search up the tree for the true map position
                }
                else
                {
                    return _position;
                }
            }
            set
            {
                var oldPosition = _position;
                _position = value;
                GridID = IoCManager.Resolve<IMapManager>().GetMap(MapID).FindGridAt(_position).Index;

                OnMove?.Invoke(this, new VectorEventArgs(oldPosition, _position));
            }
        }

        /// <inheritdoc />
        public override ComponentState GetComponentState()
        {
            return new TransformComponentState(WorldPosition, Rotation, Parent);
        }

        /// <summary>
        /// Detaches this entity from its parent.
        /// </summary>
        public void DetachParent()
        {
            // nothing to do
            if (Parent == null)
                return;

            Parent = null;
        }

        /// <summary>
        /// Sets another entity as the parent entity.
        /// </summary>
        /// <param name="parent"></param>
        public void AttachParent(ITransformComponent parent)
        {
            // nothing to attach to.
            if (parent == null)
                return;

            Parent = parent;
        }

        /// <summary>
        ///     Finds the transform of the entity located on the map itself
        /// </summary>
        public ITransformComponent GetMapTransform()
        {
            if (Parent != null) //If we are not the final transform, query up the chain of parents
            {
                return Parent.GetMapTransform();
            }
            return this;
        }

        public bool IsMapTransform(ITransformComponent transform)
        {
            if (transform.Parent != null)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        ///     Does this entity contain the entity in the argument
        /// </summary>
        public bool ContainsEntity(ITransformComponent transform)
        {
            if (IsMapTransform(transform)) //Is the entity on the map
            {
                if (this == transform.Parent) //Is this the direct container of the entity
                {
                    return true;
                }
                else
                {
                    return ContainsEntity(transform.Parent); //Recursively search up the entitys containers for this object
                }
            }
            return false;
        }
    }
}

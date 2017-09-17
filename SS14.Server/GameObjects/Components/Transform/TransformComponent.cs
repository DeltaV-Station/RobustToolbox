﻿using System;
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
        public int MapID { get; private set; } = MapManager.NULLSPACE;
        public int GridID { get; private set; } = 0;

        /// <summary>
        ///     Current rotation offset of the entity.
        /// </summary>
        public Angle Rotation { get; set; }

        /// <inheritdoc />
        public override string Name => "Transform";

        /// <inheritdoc />
        public override uint? NetID => NetIDs.TRANSFORM;

        /// <inheritdoc />
        public event EventHandler<MoveEventArgs> OnMove;

        /// <inheritdoc />
        public LocalCoordinates LocalPosition
        {
            get
            {
                if (Parent != null)
                {
                    return GetMapTransform().LocalPosition; //Search up the tree for the true map position
                }
                else
                {
                    return new LocalCoordinates(_position, GridID, MapID);
                }
            }
            set
            {
                var oldPosition = LocalPosition;
                _position = value.Position;

                MapID = value.MapID;
                GridID = value.GridID;

                OnMove?.Invoke(this, new MoveEventArgs(LocalPosition, value));
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
                    return IoCManager.Resolve<IMapManager>().GetMap(MapID).GetGrid(GridID).ConvertToWorld(_position);
                }
            }
            set
            {
                var oldPosition = LocalPosition;
                _position = value;
                GridID = IoCManager.Resolve<IMapManager>().GetMap(MapID).FindGridAt(_position).Index;

                OnMove?.Invoke(this, new MoveEventArgs(LocalPosition, new LocalCoordinates(_position, GridID, MapID)));
            }
        }

        /// <inheritdoc />
        public override ComponentState GetComponentState()
        {
            return new TransformComponentState(LocalPosition, Rotation, Parent);
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


        public bool IsMapTransform => Parent == null;

        /// <summary>
        ///     Does this entity contain the entity in the argument
        /// </summary>
        public bool ContainsEntity(ITransformComponent transform)
        {
            if (transform.IsMapTransform) //Is the entity on the map
            {
                return false;
            }

            if (this == transform.Parent) //Is this the direct container of the entity
            {
                return true;
            }
            else
            {
                return ContainsEntity(transform.Parent); //Recursively search up the entitys containers for this object
            }
        }
    }
}

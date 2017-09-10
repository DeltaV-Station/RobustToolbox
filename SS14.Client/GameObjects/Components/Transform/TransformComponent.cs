﻿using System;
using System.Collections.Generic;
using OpenTK;
using SS14.Shared;
using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.Interfaces.GameObjects.Components;
using SS14.Shared.Maths;
using SS14.Shared.Map;
using Vector2 = SS14.Shared.Maths.Vector2;

namespace SS14.Client.GameObjects
{
    /// <summary>
    ///     Stores the position and orientation of the entity.
    /// </summary>
    public class TransformComponent : Component, ITransformComponent
    {
        public Vector2 WorldPosition { get; private set; }
        public int MapID { get; private set; }
        public int GridID { get; private set; }
        public Angle Rotation { get; private set; }
        public ITransformComponent Parent { get; private set; }
        //TODO: Make parenting actually work.

        /// <inheritdoc />
        public override string Name => "Transform";

        /// <inheritdoc />
        public override uint? NetID => NetIDs.TRANSFORM;

        /// <inheritdoc />
        public override Type StateType => typeof(TransformComponentState);

        /// <inheritdoc />
        public event EventHandler<MoveEventArgs> OnMove;

        public LocalCoordinates Position => new LocalCoordinates(WorldPosition, GridID, MapID);

        /// <inheritdoc />
        public override void HandleComponentState(ComponentState state)
        {
            var newState = (TransformComponentState)state;
            Rotation = newState.Rotation;

            if (WorldPosition != newState.Position || MapID != newState.MapID || GridID != newState.GridID)
            {
                OnMove?.Invoke(this, new MoveEventArgs(Position, new LocalCoordinates(newState.Position, newState.GridID, newState.MapID)));
                WorldPosition = newState.Position;
                MapID = newState.MapID;
                GridID = newState.GridID;
            }

            if (Parent != newState.Parent)
            {
                DetachParent();
                AttachParent(newState.Parent);
            }
        }

        /// <summary>
        /// Detaches this entity from its parent.
        /// </summary>
        private void DetachParent()
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
        private void AttachParent(ITransformComponent parent)
        {
            // nothing to attach to.
            if (parent == null)
                return;

            Parent = parent;
        }

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

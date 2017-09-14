﻿using System;
using SS14.Shared.Maths;
using SS14.Shared.Map;

namespace SS14.Shared.Interfaces.GameObjects.Components
{
    /// <summary>
    ///     Stores the position and orientation of the entity.
    /// </summary>
    public interface ITransformComponent : IComponent
    {
        /// <summary>
        ///     Current position offset of the entity.
        /// </summary>
        LocalCoordinates LocalPosition { get; }

        /// <summary>
        ///     Current position offset of the entity.
        /// </summary>
        Vector2 WorldPosition { get; }

        /// <summary>
        ///     Current rotation offset of the entity.
        /// </summary>
        Angle Rotation { get; }

        /// <summary>
        ///     Event that gets invoked every time the position gets modified through properties such as <see cref="Rotation" />.
        /// </summary>
        event EventHandler<MoveEventArgs> OnMove;

        /// <summary>
        ///     Reference to the transform of the container of this object if it exists, can be nested several times.
        /// </summary>
        ITransformComponent Parent { get; }

        /// <summary>
        ///     Finds the transform located on the map or in nullspace
        /// </summary>
        ITransformComponent GetMapTransform();

        /// <summary>
        ///     Returns whether the entity of this transform contains the entity argument
        /// </summary>
        bool ContainsEntity(ITransformComponent entity);

        /// <summary>
        ///     Returns the index of the map which this object is on
        /// </summary>
        int MapID { get; }

        /// <summary>
        ///     Returns the index of the grid which this object is on
        /// </summary>
        int GridID { get; }
    }
}

﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Robust.Shared.GameObjects.Components;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;

namespace Robust.Shared.Interfaces.Physics
{
    /// <summary>
    ///     This service provides access into the physics system.
    /// </summary>
    public interface IPhysicsManager
    {
        /// <summary>
        /// Checks to see if the specified collision rectangle collides with any of the physBodies under management.
        /// Also fires the OnCollide event of the first managed physBody to intersect with the collider.
        /// </summary>
        /// <param name="collider">Collision rectangle to check</param>
        /// <param name="map">Map to check on</param>
        /// <returns>true if collides, false if not</returns>
        bool TryCollideRect(Box2 collider, MapId map);

        /// <summary>
        /// Get all entities colliding with a certain body.
        /// </summary>
        /// <param name="body"></param>
        /// <returns></returns>
        IEnumerable<IEntity> GetCollidingEntities(IPhysBody body, Vector2 offset, bool approximate = true);

        /// <summary>
        ///     Checks whether a body is colliding
        /// </summary>
        /// <param name="body"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        bool IsColliding(IPhysBody body, Vector2 offset);

        void AddBody(IPhysBody physBody);
        void RemoveBody(IPhysBody physBody);

        /// <summary>
        ///     Casts a ray in the world and returns the first thing it hit.
        /// </summary>
        /// <param name="mapId"></param>
        /// <param name="ray">Ray to cast in the world.</param>
        /// <param name="maxLength">Maximum length of the ray in meters.</param>
        /// <param name="ignoredEnt">A single entity that can be ignored by the RayCast. Useful if the ray starts inside the body of an entity.</param>
        /// <param name="ignoreNonHardCollidables">If true, the RayCast will ignore any bodies that aren't hard collidables.</param>
        /// <returns>A result object describing the hit, if any.</returns>
        RayCastResults IntersectRay(MapId mapId, CollisionRay ray, float maxLength = 50, IEntity ignoredEnt = null);


        /// <summary>
        ///     Calculates the normal vector for two colliding bodies
        /// </summary>
        /// <param name="target"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        Vector2 CalculateNormal(ICollidableComponent target, ICollidableComponent source);


        /// <summary>
        ///     Applies impulses to two colliding bodies, returning the accumulated impulse for both.
        /// </summary>
        /// <param name="aC"></param>
        /// <param name="bC"></param>
        /// <param name="aP"></param>
        /// <param name="bP"></param>
        /// <param name="contactCount"></param>
        /// <returns>A impulse vector in kilogram meters per second</returns>
        public void SolveCollisionImpulse(ICollidableComponent aC, ICollidableComponent bC,
            [CanBeNull] SharedPhysicsComponent aP, [CanBeNull] SharedPhysicsComponent bP);

        /// <summary>
        ///     Casts a ray in the world and returns the first thing it hit.
        /// </summary>
        /// <param name="mapId"></param>
        /// <param name="ray">Ray to cast in the world.</param>
        /// <param name="maxLength">Maximum length of the ray in meters.</param>
        /// <param name="predicate">A predicate to check whether to ignore an entity or not. If it returns true, it will be ignored.</param>
        /// <param name="ignoreNonHardCollidables">If true, the RayCast will ignore any bodies that aren't hard collidables.</param>
        /// <returns>A result object describing the hit, if any.</returns>
        RayCastResults IntersectRayWithPredicate(MapId mapId, CollisionRay ray, float maxLength = 50, Func<IEntity, bool> predicate = null);

        event Action<DebugRayData> DebugDrawRay;

        IEnumerable<(IPhysBody, IPhysBody)> GetCollisions();

        bool Update(IPhysBody collider);

        void RemovedFromMap(IPhysBody body, MapId mapId);
        void AddedToMap(IPhysBody body, MapId mapId);
    }

    public struct DebugRayData
    {
        public DebugRayData(Ray ray, float maxLength, RayCastResults results)
        {
            Ray = ray;
            MaxLength = maxLength;
            Results = results;
        }

        public Ray Ray { get; }
        public RayCastResults Results { get; }
        public float MaxLength { get; }
    }
}

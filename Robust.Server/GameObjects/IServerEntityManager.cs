﻿using Robust.Shared.GameObjects;
using System.Collections.Generic;
using Robust.Server.Player;
using Robust.Shared.Timing;

namespace Robust.Server.GameObjects
{
    public interface IServerEntityManager : IEntityManager
    {
        /// <summary>
        ///     Gets all entity states that have been modified after and including the provided tick.
        /// </summary>
        List<EntityState>? GetAllEntityStates(GameTick fromTick, IPlayerSession? session);

        /// <summary>
        ///     Gets all entity states within an AABB that have been modified after and including the provided tick.
        /// </summary>
        List<EntityState>? GetEntityStates(GameTick fromTick, GameTick currentTick, IPlayerSession player, float range);

        // Keep track of deleted entities so we can sync deletions with the client.
        /// <summary>
        ///     Gets a list of all entity UIDs that were deleted between <paramref name="fromTick" /> and now.
        /// </summary>
        List<EntityUid>? GetDeletedEntities(GameTick fromTick);

        /// <summary>
        ///     Remove deletion history.
        /// </summary>
        /// <param name="toTick">The last tick to delete the history for. Inclusive.</param>
        void CullDeletionHistory(GameTick toTick);

        float MaxUpdateRange { get; }
    }
}

﻿using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.GameObjects;

namespace SS14.Server.Interfaces.GameObjects
{
    interface IServerEntityManagerInternal : IServerEntityManager
    {
        // These methods are used by the map loader to do multi-stage entity construction during map load.
        // I would recommend you refer to the MapLoader for usage.

        IEntity AllocEntity(string prototypeName, EntityUid? uid = null);

        void FinishEntityLoad(IEntity entity, IEntityLoadContext context = null);

        void FinishEntityInitialization(IEntity entity);

        void FinishEntityStartup(IEntity entity);
    }
}

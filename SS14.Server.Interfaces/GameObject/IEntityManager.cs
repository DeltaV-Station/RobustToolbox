﻿using Lidgren.Network;
using SFML.System;
using SS14.Shared.GameObjects;
using SS14.Shared.GO;
using SS14.Shared.IoC;
using System.Collections.Generic;

namespace SS14.Server.Interfaces.GOC
{
    public interface IEntityManager : SS14.Shared.GameObjects.IEntityManager, IIoCInterface
    {
        void Shutdown();
        void HandleEntityNetworkMessage(NetIncomingMessage message);
        void SaveEntities();
        Entity SpawnEntity(string template, int uid = -1);
        Entity SpawnEntityAt(string entityTemplateName, Vector2f vector2);
        List<EntityState> GetEntityStates();
        void Update(float frameTime);
    }
}

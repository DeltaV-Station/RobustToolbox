﻿using OpenTK;
using SFML.System;
using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.IoC;
using System.Collections.Generic;

namespace SS14.Client.Interfaces.GameObjects
{
    public interface IClientEntityManager : IEntityManager
    {
        IEnumerable<IEntity> GetEntitiesInRange(Vector2 position, float Range);
        void ApplyEntityStates(IEnumerable<EntityState> entityStates, float serverTime);
    }
}

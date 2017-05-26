﻿using SS14.Shared.GameObjects;
using SS14.Shared.GameObjects.System;

namespace SS14.Server.GameObjects.EntitySystems
{
    public class TestSystem : EntitySystem
    {
        public TestSystem(EntityManager em, EntitySystemManager esm)
            : base(em, esm)
        {
            EntityQuery = new EntityQuery();
            EntityQuery.OneSet.Add(typeof (BasicItemComponent));
        }

        public override void Update(float frametime)
        {
        }
    }
}
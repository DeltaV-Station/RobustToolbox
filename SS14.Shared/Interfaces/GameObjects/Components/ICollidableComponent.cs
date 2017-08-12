﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS14.Shared.Interfaces.Physics;

namespace SS14.Shared.Interfaces.GameObjects.Components
{
    public interface ICollidableComponent : IComponent, ICollidable
    {
    }
}

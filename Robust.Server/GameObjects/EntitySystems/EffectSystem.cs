﻿using DataStructures;
using Robust.Shared.GameObjects.EntitySystemMessages;
using Robust.Shared.GameObjects.Systems;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using System.Collections.Generic;

namespace Robust.Server.GameObjects.EntitySystems
{
    /// <summary>
    /// An entity system that displays temporary effects to the user
    /// </summary>
    public class EffectSystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _timing;

        /// <summary>
        /// Priority queue sorted by how soon the effect will die, we remove messages from the front of the queue during update until caught up
        /// </summary>
        private readonly PriorityQueue<EffectSystemMessage> _CurrentEffects = new PriorityQueue<EffectSystemMessage>(new EffectMessageComparer());

        public void CreateParticle(EffectSystemMessage effect)
        {
            _CurrentEffects.Add(effect);

            //For now we will use this which sends to ALL clients
            //TODO: Client bubbling
            RaiseNetworkEvent(effect);
        }

        public override void Update(float frameTime)
        {
            //Take elements from front of priority queue until they are old
            while (_CurrentEffects.Count != 0 && _CurrentEffects.Peek().DeathTime < _timing.CurTime)
            {
                _CurrentEffects.Take();
            }
        }

        /// <summary>
        /// Comparer that keeps the device dictionary sorted by powernet priority
        /// </summary>
        public class EffectMessageComparer : IComparer<EffectSystemMessage>
        {
            public int Compare(EffectSystemMessage x, EffectSystemMessage y)
            {
                return y.DeathTime.CompareTo(x.DeathTime);
            }
        }

        //TODO: Send all current effects to new clients on login
        //TODO: Send effects only to relevant client bubbles
    }
}

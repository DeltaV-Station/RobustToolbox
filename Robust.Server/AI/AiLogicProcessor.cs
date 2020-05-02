﻿using Robust.Shared.Interfaces.GameObjects;

namespace Robust.Server.AI
{
    /// <summary>
    ///     Base class for all AI Processors.
    /// </summary>
    public abstract class AiLogicProcessor
    {
        /// <summary>
        ///     Radius in meters that the AI can "see".
        /// </summary>
        public float VisionRadius { get; set; }

        /// <summary>
        ///     Entity this AI is controlling.
        /// </summary>
        public IEntity SelfEntity { get; set; }

        /// <summary>
        ///     One-Time setup when the processor is created.
        /// </summary>
        public virtual void Setup() { }

        /// <summary>
        /// One-Time shutdown when processor is done
        /// </summary>
        public virtual void Shutdown() {}

        /// <summary>
        ///     Gives life to the AI.
        /// </summary>
        /// <param name="frameTime">Time since last update in seconds.</param>
        public abstract void Update(float frameTime);
    }
}

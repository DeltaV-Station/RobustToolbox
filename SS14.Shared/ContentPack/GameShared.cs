﻿namespace SS14.Shared.ContentPack
{
    /// <summary>
    ///     Common entry point for Content assemblies.
    /// </summary>
    public abstract class GameShared
    {
        public virtual void Init()
        {
        }

        public virtual void Update(AssemblyLoader.UpdateLevel level, float frameTime)
        {
        }
    }
}

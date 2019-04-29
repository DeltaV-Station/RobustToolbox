﻿using Robust.Shared.Timing;

namespace Robust.Shared.Map
{
    /// <inheritdoc />
    internal interface IMapChunkInternal : IMapChunk
    {
        /// <summary>
        ///     The last game simulation tick that this chunk was modified.
        /// </summary>
        GameTick LastModifiedTick { get; }
    }
}

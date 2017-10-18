﻿using System.IO;
using SS14.Client.Graphics.Textures;
using SS14.Client.Interfaces.Resource;
using SS14.Client.ResourceManagement;

namespace SS14.Client.ResourceManagment
{
    /// <summary>
    ///     Holds a SFML Texture resource in the cache.
    /// </summary>
    class TextureResource : BaseResource
    {
        /// <inheritdoc />
        public override string Fallback => @"Textures/noSprite.png";

        public Texture Texture { get; private set; }

        /// <inheritdoc />
        public override void Load(IResourceCache cache, string path, Stream stream)
        {
            Texture = new Texture(stream);
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            Texture.Dispose();
        }
    }
}

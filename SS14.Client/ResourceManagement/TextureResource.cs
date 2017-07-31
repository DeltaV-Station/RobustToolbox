﻿using System.IO;
using SFML.Graphics;
using SS14.Client.ResourceManagement;
using SS14.Client.Resources;

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
        public override void Load(ResourceCache cache, string path, Stream stream)
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

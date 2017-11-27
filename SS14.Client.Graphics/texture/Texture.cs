﻿using SS14.Client.Graphics.Sprites;
using SS14.Shared.Maths;
using SS14.Client.Graphics.Utility;
using STexture = SFML.Graphics.Texture;
using System.IO;
using System;

namespace SS14.Client.Graphics.Textures
{
    public class Texture : IDisposable
    {
        public STexture SFMLTexture { get; }
        public Vector2u Size => SFMLTexture.Size.Convert();

        public Texture(uint width, uint height)
        {
            SFMLTexture = new STexture(width, height);
        }

        public Texture(Stream stream)
        {
            if (!stream.CanSeek || !stream.CanRead)
            {
                throw new ArgumentException("Stream must be read and seekable.", nameof(stream));
            }

            SFMLTexture = new STexture(stream);
        }

        public Texture(Image image)
        {
            SFMLTexture = new STexture(image.SFMLImage);
        }

        public Texture(byte[] bytes)
        {
            SFMLTexture = new STexture(bytes);
        }

        internal Texture(STexture sfmlTexture)
        {
            SFMLTexture = sfmlTexture;
        }

        public bool Smooth
        {
            get => SFMLTexture.Smooth;
            set => SFMLTexture.Smooth = value;
        }

        public void Dispose() => SFMLTexture.Dispose();

        /// <summary>
        ///     Copies the contents of this <c>RenderImage</c> into an <see cref="Image" />,
        ///     THIS IS A VERY SLOW OPERATION. DO NOT DO THIS IN PERFORMANCE CRITICAL CODE.
        /// </summary>
        public Image CopyToImage()
        {
            return new Image(SFMLTexture.CopyToImage());
        }
    }
}

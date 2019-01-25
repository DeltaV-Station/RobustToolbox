﻿using System;
using System.IO;
using JetBrains.Annotations;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SS14.Client.Interfaces.Graphics;
using SS14.Client.Utility;
using SS14.Shared.IoC;
using SS14.Shared.Maths;
using SS14.Shared.Utility;

namespace SS14.Client.Graphics
{
    /// <summary>
    ///     Contains a texture used for drawing things.
    /// </summary>
    public abstract class Texture : IDirectionalTextureProvider
    {
        internal abstract Godot.Texture GodotTexture { get; }

        public abstract int Width { get; }
        public abstract int Height { get; }

        public Vector2i Size => new Vector2i(Width, Height);

        public static implicit operator Godot.Texture(Texture src)
        {
            return src?.GodotTexture;
        }

        public static Texture LoadFromImage<T>(Image<T> image) where T : struct, IPixel<T>
        {
            switch (GameController.Mode)
            {
                case GameController.DisplayMode.Headless:
                    return new BlankTexture();
                case GameController.DisplayMode.Godot:
                {
                    var stream = new MemoryStream();

                    try
                    {
                        image.SaveAsPng(stream, new PngEncoder {CompressionLevel = 1});

                        var gdImage = new Godot.Image();
                        var ret = gdImage.LoadPngFromBuffer(stream.ToArray());
                        if (ret != Godot.Error.Ok)
                        {
                            throw new InvalidDataException(ret.ToString());
                        }

                        // Godot does not provide a way to load from memory directly so we turn it into a PNG I guess.
                        var texture = new Godot.ImageTexture();
                        texture.CreateFromImage(gdImage);
                        return new GodotTextureSource(texture);
                    }
                    finally
                    {
                        stream.Dispose();
                    }
                }
                case GameController.DisplayMode.OpenGL:
                {
                    var manager = IoCManager.Resolve<IDisplayManagerOpenGL>();
                    return manager.LoadTextureFromImage(image);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static Texture LoadFromPNGStream(Stream stream)
        {
            switch (GameController.Mode)
            {
                case GameController.DisplayMode.Headless:
                    return new BlankTexture();
                case GameController.DisplayMode.Godot:
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);

                        var data = memoryStream.ToArray();
                        var gdImage = new Godot.Image();
                        var ret = gdImage.LoadPngFromBuffer(data);
                        if (ret != Godot.Error.Ok)
                        {
                            throw new InvalidDataException(ret.ToString());
                        }

                        var texture = new Godot.ImageTexture();
                        texture.CreateFromImage(gdImage);
                        return new GodotTextureSource(texture);
                    }
                case GameController.DisplayMode.OpenGL:
                {
                    var manager = IoCManager.Resolve<IDisplayManagerOpenGL>();
                    return manager.LoadTextureFromPNGStream(stream);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        Texture IDirectionalTextureProvider.Default => this;

        Texture IDirectionalTextureProvider.TextureFor(Direction dir)
        {
            return this;
        }
    }

    /// <summary>
    ///     Blank dummy texture.
    /// </summary>
    public class BlankTexture : Texture
    {
        internal override Godot.Texture GodotTexture => null;
        public override int Width => default;
        public override int Height => default;
    }

    [PublicAPI]
    public sealed class AtlasTexture : Texture
    {
        public AtlasTexture(Texture texture, UIBox2 subRegion)
        {
            if (GameController.OnGodot)
            {
                GodotTexture = new Godot.AtlasTexture
                {
                    Atlas = texture,
                    Region = subRegion.Convert()
                };
            }

            SubRegion = subRegion;
            SourceTexture = texture;
        }

        internal override Godot.Texture GodotTexture { get; }
        internal Texture SourceTexture { get; }
        public UIBox2 SubRegion { get; }
        public override int Width => (int) SubRegion.Width;
        public override int Height => (int) SubRegion.Height;
    }

    internal class OpenGLTexture : Texture
    {
        internal override Godot.Texture GodotTexture => null;
        internal int OpenGLTextureId { get; }

        public override int Width { get; }
        public override int Height { get; }

        internal OpenGLTexture(int id, int width, int height)
        {
            OpenGLTextureId = id;
            Width = width;
            Height = height;
        }
    }

    /// <summary>
    ///     Wraps a texture returned by Godot itself,
    ///     for example when the texture was set in a GUI scene.
    /// </summary>
    internal class GodotTextureSource : Texture
    {
        internal override Godot.Texture GodotTexture { get; }
        public override int Width => GodotTexture.GetWidth();
        public override int Height => GodotTexture.GetHeight();

        public GodotTextureSource(Godot.Texture texture)
        {
            DebugTools.Assert(GameController.OnGodot);
            GodotTexture = texture;
        }
    }
}

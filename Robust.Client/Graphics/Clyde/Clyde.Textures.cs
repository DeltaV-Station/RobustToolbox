using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using OpenTK.Graphics.OpenGL;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using OGLTextureWrapMode = OpenTK.Graphics.OpenGL.TextureWrapMode;

namespace Robust.Client.Graphics.Clyde
{
    internal partial class Clyde
    {
        private readonly Dictionary<int, LoadedTexture> _loadedTextures = new Dictionary<int, LoadedTexture>();
        private int _nextTextureId;

        public Texture LoadTextureFromPNGStream(Stream stream, string name = null,
            TextureLoadParameters? loadParams = null)
        {
            DebugTools.Assert(_mainThread == Thread.CurrentThread);

            using (var image = Image.Load(stream))
            {
                return LoadTextureFromImage(image, name, loadParams);
            }
        }

        public Texture LoadTextureFromImage<T>(Image<T> image, string name = null,
            TextureLoadParameters? loadParams = null) where T : unmanaged, IPixel<T>
        {
            DebugTools.Assert(_mainThread == Thread.CurrentThread);

            var pixelType = typeof(T);

            var texture = new OGLHandle((uint) GL.GenTexture());
            GL.BindTexture(TextureTarget.Texture2D, texture.Handle);
            _applySampleParameters(loadParams?.SampleParameters);

            PixelInternalFormat internalFormat;
            PixelFormat pixelDataFormat;
            PixelType pixelDataType;

            if (pixelType == typeof(Rgba32))
            {
                internalFormat = PixelInternalFormat.Srgb8Alpha8;
                pixelDataFormat = PixelFormat.Rgba;
                pixelDataType = PixelType.UnsignedByte;
            }
            else if (pixelType == typeof(Alpha8))
            {
                if (image.Width % 4 != 0 || image.Height % 4 != 0)
                {
                    throw new ArgumentException("Alpha8 images must have multiple of 4 sizes.");
                }
                internalFormat = PixelInternalFormat.R8;
                pixelDataFormat = PixelFormat.Red;
                pixelDataType = PixelType.UnsignedByte;

                // TODO: Does it make sense to default to 1 for RGB parameters?
                // It might make more sense to pass some options to change swizzling.
                var swizzle = new[] {(int) All.One, (int) All.One, (int) All.One, (int) All.Red};
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleRgba, swizzle);
            }
            else
            {
                throw new NotImplementedException($"Unable to handle pixel type '{pixelType.Name}'");
            }

            unsafe
            {
                var span = image.GetPixelSpan();
                fixed (T* ptr = span)
                {
                    GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, image.Width, image.Height, 0,
                        pixelDataFormat, pixelDataType, (IntPtr) ptr);
                }
            }

            return _genTexture(texture, (image.Width, image.Height), name);
        }

        private static void _applySampleParameters(TextureSampleParameters? sampleParameters)
        {
            var actualParams = sampleParameters ?? TextureSampleParameters.Default;
            if (actualParams.Filter)
            {
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                    (int) TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                    (int) TextureMagFilter.Linear);
            }
            else
            {
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                    (int) TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter,
                    (int) TextureMagFilter.Nearest);
            }

            switch (actualParams.WrapMode)
            {
                case TextureWrapMode.None:
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                        (int) OGLTextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                        (int) OGLTextureWrapMode.ClampToEdge);
                    break;
                case TextureWrapMode.Repeat:
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                        (int) OGLTextureWrapMode.Repeat);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                        (int) OGLTextureWrapMode.Repeat);
                    break;
                case TextureWrapMode.MirroredRepeat:
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS,
                        (int) OGLTextureWrapMode.MirroredRepeat);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT,
                        (int) OGLTextureWrapMode.MirroredRepeat);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private ClydeTexture _genTexture(OGLHandle oglHandle, Vector2i size, string name)
        {
            if (name != null)
            {
                _objectLabelMaybe(ObjectLabelIdentifier.Texture, oglHandle, name);
            }

            var (width, height) = size;

            var loaded = new LoadedTexture
            {
                OpenGLObject = oglHandle,
                Width = width,
                Height = height,
                Name = name
            };

            var id = ++_nextTextureId;
            _loadedTextures.Add(id, loaded);

            return new ClydeTexture(id, width, height, this);
        }

        private void _deleteTexture(ClydeTexture texture)
        {
            if (!_loadedTextures.TryGetValue(texture.TextureId, out var loadedTexture))
            {
                // Already deleted.
                return;
            }

            GL.DeleteTexture(loadedTexture.OpenGLObject.Handle);
            _loadedTextures.Remove(texture.TextureId);
        }

        private void _loadStockTextures()
        {
            var white = new Image<Rgba32>(1, 1);
            white[0, 0] = Rgba32.White;
            Texture.White = Texture.LoadFromImage(white);

            var blank = new Image<Rgba32>(1, 1);
            blank[0, 0] = Rgba32.Transparent;
            Texture.Transparent = Texture.LoadFromImage(blank);
        }

        private sealed class LoadedTexture
        {
            public OGLHandle OpenGLObject;
            public int Width;
            public int Height;
            public string Name;
            public Vector2i Size => (Width, Height);
        }

        private sealed class ClydeTexture : OwnedTexture
        {
            private readonly Clyde _clyde;

            internal int TextureId { get; }

            public override int Width { get; }
            public override int Height { get; }

            public override void Delete()
            {
                _clyde._deleteTexture(this);
            }

            internal ClydeTexture(int id, int width, int height, Clyde clyde)
            {
                TextureId = id;
                Width = width;
                Height = height;
                _clyde = clyde;
            }
        }
    }
}


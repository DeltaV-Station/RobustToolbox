using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using OpenTK.Graphics.OpenGL4;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using OGLTextureWrapMode = OpenTK.Graphics.OpenGL4.TextureWrapMode;

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

            var texture = new OGLHandle(GL.GenTexture());
            GL.BindTexture(TextureTarget.Texture2D, texture.Handle);
            var actualParams = loadParams ?? TextureLoadParameters.Default;
            if (actualParams.SampleParameters.Filter)
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

            switch (actualParams.SampleParameters.WrapMode)
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

            PixelInternalFormat internalFormat;
            PixelFormat pixelDataFormat;
            PixelType pixelDataType;

            if (pixelType == typeof(Rgba32))
            {
                internalFormat = PixelInternalFormat.Rgba8;
                pixelDataFormat = PixelFormat.Rgba;
                pixelDataType = PixelType.UnsignedByte;
            }
            else if (pixelType == typeof(Alpha8))
            {
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
                var length = span.Length;
                var val1 = span[0];
                fixed (T* ptr = span)
                {
                    GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, image.Width, image.Height, 0,
                        pixelDataFormat, pixelDataType, (IntPtr) ptr);
                }
            }

            if (name != null)
            {
                _objectLabelMaybe(ObjectLabelIdentifier.Texture, texture, name);
            }

            var loaded = new LoadedTexture
            {
                OpenGLObject = texture,
                Width = image.Width,
                Height = image.Height,
                Name = name,
                Type = LoadedTextureType.Texture2D
            };

            var id = ++_nextTextureId;
            _loadedTextures.Add(id, loaded);

            return new OpenGLTexture(id, image.Width, image.Height);
        }

        public TextureArray LoadArrayFromImages<T>(ICollection<Image<T>> images, string name = null,
            TextureLoadParameters? loadParams = null)
            where T : unmanaged, IPixel<T>
        {
            DebugTools.Assert(images.Count != 0);
            (int x, int y)? size = null;
            foreach (var image in images)
            {
                if (size == null)
                {
                    size = (image.Width, image.Height);
                }

                if (size.Value.x != image.Width || size.Value.y != image.Height)
                {
                    throw new ArgumentException("All images must be of the same dimensions.", nameof(images));
                }
            }

            var textureId = ++_nextTextureId;
            var texture = new OGLHandle(GL.GenTexture());
            GL.BindTexture(TextureTarget.Texture2DArray, texture.Handle);
            if (name != null)
            {
                _objectLabelMaybe(ObjectLabelIdentifier.Texture, texture, name);
            }

            DebugTools.Assert(size.HasValue);
            var (width, height) = size.Value;
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, PixelInternalFormat.Rgba8, width, height, images.Count, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            var actualParams = loadParams ?? TextureLoadParameters.Default;
            if (actualParams.SampleParameters.Filter)
            {
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter,
                    (int) TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter,
                    (int) TextureMagFilter.Linear);
            }
            else
            {
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter,
                    (int) TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter,
                    (int) TextureMagFilter.Nearest);
            }

            switch (actualParams.SampleParameters.WrapMode)
            {
                case TextureWrapMode.None:
                    GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS,
                        (int) OGLTextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT,
                        (int) OGLTextureWrapMode.ClampToEdge);
                    break;
                case TextureWrapMode.Repeat:
                    GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS,
                        (int) OGLTextureWrapMode.Repeat);
                    GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT,
                        (int) OGLTextureWrapMode.Repeat);
                    break;
                case TextureWrapMode.MirroredRepeat:
                    GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS,
                        (int) OGLTextureWrapMode.MirroredRepeat);
                    GL.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT,
                        (int) OGLTextureWrapMode.MirroredRepeat);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var index = 0;
            var refTextureList = new List<OpenGLTexture>();
            foreach (var image in images)
            {
                var span = ((Image<Rgba32>) (object) image).GetPixelSpan();
                unsafe
                {
                    fixed (Rgba32* ptr = span)
                    {
                        GL.TexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, index, width, height, 1,
                            PixelFormat.Rgba, PixelType.UnsignedByte, (IntPtr) ptr);
                    }
                }

                index += 1;
                refTextureList.Add(new OpenGLTexture(textureId, width, height, index));
            }

            var loaded = new LoadedTexture
            {
                OpenGLObject = texture,
                Width = width,
                Height = height,
                ArrayDepth = images.Count,
                Name = name,
                Type = LoadedTextureType.Array2D
            };

            _loadedTextures.Add(textureId, loaded);
            return new TextureArray(refTextureList.ToArray());
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

        private class LoadedTexture
        {
            public OGLHandle OpenGLObject;
            public LoadedTextureType Type;
            public int Width;
            public int Height;
            public int? ArrayDepth;
            public string Name;
            public Vector2i Size => new Vector2i(Width, Height);
        }

        private enum LoadedTextureType
        {
            Texture2D,
            Array2D,
        }
    }
}

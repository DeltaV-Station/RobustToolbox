using System;
using System.IO;
using Robust.Client.Audio;
using Robust.Client.Graphics.Shaders;
using Robust.Client.Interfaces.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Robust.Client.Graphics.Clyde
{
    /// <summary>
    ///     Hey look, it's Clyde's evil twin brother!
    /// </summary>
    internal sealed class ClydeHeadless : DisplayManager, IClydeInternal, IClydeAudio
    {
        // Would it make sense to report a fake resolution like 720p here so code doesn't break? idk.
        public override Vector2i ScreenSize { get; set; } = (1280, 720);
        public ShaderInstance InstanceShader(ClydeHandle handle)
        {
            return new DummyShaderInstance();
        }

        public Vector2 MouseScreenPosition => ScreenSize / 2;
        public IClydeDebugInfo DebugInfo => null;

        public override void SetWindowTitle(string title)
        {
            // Nada.
        }

        public override void Initialize(bool lite=false)
        {
            // Nada.
        }

#pragma warning disable CS0067
        public override event Action<WindowResizedEventArgs> OnWindowResized;
#pragma warning restore CS0067

        public void Render()
        {
            // Nada.
        }

        public void FrameProcess(FrameEventArgs eventArgs)
        {
            // Nada.
        }

        public void ProcessInput(FrameEventArgs frameEventArgs)
        {
            // Nada.
        }

        public Texture LoadTextureFromPNGStream(Stream stream, string name = null,
            TextureLoadParameters? loadParams = null)
        {
            using (var image = Image.Load(stream))
            {
                return LoadTextureFromImage(image, name, loadParams);
            }
        }

        public Texture LoadTextureFromImage<T>(Image<T> image, string name = null,
            TextureLoadParameters? loadParams = null) where T : unmanaged, IPixel<T>
        {
            return new DummyTexture((image.Width, image.Height));
        }

        public IRenderTarget CreateRenderTarget(Vector2i size, RenderTargetColorFormat colorFormat,
            TextureSampleParameters? sampleParameters = null, string name = null)
        {
            return new DummyRenderTarget(size, new DummyTexture(size));
        }

        public ClydeHandle LoadShader(ParsedShader shader, string name = null)
        {
            return default;
        }

        public void Ready()
        {
            // Nada.
        }

        public AudioStream LoadAudioOggVorbis(Stream stream, string name = null)
        {
            // TODO: Might wanna actually load this so the length gets reported correctly.
            return new AudioStream(default, default, 1, name);
        }

        public AudioStream LoadAudioWav(Stream stream, string name = null)
        {
            // TODO: Might wanna actually load this so the length gets reported correctly.
            return new AudioStream(default, default, 1, name);
        }

        public IClydeAudioSource CreateAudioSource(AudioStream stream)
        {
            return DummyAudioSource.Instance;
        }

        public IClydeBufferedAudioSource CreateBufferedAudioSource(int buffers)
        {
            return DummyBufferedAudioSource.Instance;
        }

        public IntPtr GetNativeWindowHandle()
        {
            return default;
        }

        private class DummyAudioSource : IClydeAudioSource
        {
            public static DummyAudioSource Instance { get; } = new DummyAudioSource();
            public bool IsPlaying => default;

            public void Dispose()
            {
                // Nada.
            }

            public void StartPlaying()
            {
                // Nada.
            }

            public void SetPosition(Vector2 position)
            {
                // Nada.
            }

            public void SetPitch(float pitch)
            {
                // Nada.
            }

            public void SetGlobal()
            {
                // Nada.
            }

            public void SetVolume(float decibels)
            {
                // Nada.
            }
        }

        private sealed class DummyBufferedAudioSource : DummyAudioSource, IClydeBufferedAudioSource
        {
            public new static DummyBufferedAudioSource Instance { get; } = new DummyBufferedAudioSource();
            public int SampleRate { get; set; } = 0;

            public void WriteBuffer(uint handle, ReadOnlySpan<ushort> data)
            {
                // Nada.
            }

            public void QueueBuffer(uint handle)
            {
                // Nada.
            }

            public void QueueBuffers(ReadOnlySpan<uint> handles)
            {
                // Nada.
            }

            public void EmptyBuffers()
            {
                // Nada.
            }

            public void GetBuffersProcessed(Span<uint> handles)
            {
                // Nada.
            }

            public int GetNumberOfBuffersProcessed()
            {
                return 0;
            }
        }

        private sealed class DummyTexture : OwnedTexture
        {
            public override void Delete()
            {
                // Hey that was easy.
            }

            public DummyTexture(Vector2i size) : base(size)
            {
            }
        }

        private sealed class DummyShaderInstance : ShaderInstance
        {
            public override void SetParameter(string name, float value)
            {
            }

            public override void SetParameter(string name, Vector2 value)
            {
            }

            public override void SetParameter(string name, Vector3 value)
            {
            }

            public override void SetParameter(string name, Vector4 value)
            {
            }

            public override void SetParameter(string name, int value)
            {
            }

            public override void SetParameter(string name, Vector2i value)
            {
            }

            public override void SetParameter(string name, bool value)
            {
            }

            public override void SetParameter(string name, in Matrix3 matrix)
            {
            }

            public override void SetParameter(string name, in Matrix4 matrix)
            {
            }

            public override void SetParameter(string name, Texture texture)
            {
            }

            public override void Dispose()
            {
            }
        }

        private sealed class DummyRenderTarget : IRenderTarget
        {
            public DummyRenderTarget(Vector2i size, Texture texture)
            {
                Size = size;
                Texture = texture;
            }

            public Vector2i Size { get; }
            public Texture Texture { get; }

            public void Delete()
            {
            }
        }
    }
}

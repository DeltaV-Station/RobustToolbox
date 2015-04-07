﻿using GorgonLibrary;
using GorgonLibrary.Graphics;
using SS14.Client.Interfaces.Resource;
using SS14.Client.Interfaces.Utility;
using SS14.Shared;
using SS14.Shared.Utility;

namespace SS14.Client.Services.Player.PostProcessing
{
    public class AcidPostProcessingEffect : PostProcessingEffect
    {
        private readonly ShaderTechnique _shader;
        private readonly RenderImage copyImage;
        private Image _noiseBase;

        public AcidPostProcessingEffect(float duration)
            : base(duration)
        {
            _shader = IoCManager.Resolve<IResourceManager>().GetShader("acid2").Techniques["PerlinNoise"];
            copyImage = new RenderImage("perlinnoiseimage" + RandomString.Generate(10),
                                        Gorgon.CurrentClippingViewport.Width, Gorgon.CurrentClippingViewport.Height,
                                        ImageBufferFormats.BufferRGB888A8);
        }

        public override void ProcessImage(RenderImage image)
        {
            if (_noiseBase == null)
                GenerateNoise(32);

            var shadowColor = new Vector4D(1, 0, 0, 1);
            var midtoneColor = new Vector4D(0, 0, 1, 1);
            var highlightColor = new Vector4D(0, 1, 0, 1);
            Gorgon.CurrentRenderTarget = copyImage;
            Gorgon.CurrentShader = _shader;
            _shader.Parameters["xTime"].SetValue(_duration/20);
            _shader.Parameters["xOvercast"].SetValue(1.0f);
            _shader.Parameters["NoiseTexture"].SetValue(_noiseBase);
            _shader.Parameters["SceneTexture"].SetValue(image);
            //_shader.Parameters["shadowColor"].SetValue(shadowColor);
            //_shader.Parameters["midtoneColor"].SetValue(midtoneColor);
            //_shader.Parameters["highlightColor"].SetValue(highlightColor);

            _noiseBase.Blit(0, 0, image.Width, image.Height);
            Gorgon.CurrentShader = null;
            Gorgon.CurrentRenderTarget = null;

            image.CopyFromImage(copyImage.Image);
        }

        private void GenerateNoise(int resolution)
        {
            if (_noiseBase != null)
            {
                _noiseBase.Dispose();
                _noiseBase = null;
            }
            var rand = IoCManager.Resolve<IRand>();
            var noisyColors = new byte[resolution*resolution*4];
            byte b;
            for (int x = 0; x < resolution; x++)
                for (int y = 0; y < resolution; y++)
                {
                    //noisyColors[x + y * resolution] = new Color(new Vector3((float)rand.Next(1000) / 1000.0f, 0, 0));
                    b = (byte) rand.Next(255);
                    noisyColors[x*4 + y*resolution*4] = b;
                    noisyColors[x*4 + y*resolution*4 + 1] = b;
                    noisyColors[x*4 + y*resolution*4 + 2] = b;
                    noisyColors[x*4 + y*resolution*4 + 3] = 255;
                }

            //_noiseBase = GorgonLibrary.Graphics.Image.FromStream("perlinnoisebase" + RandomString.Generate(6), new MemoryStream(noisyColors),resolution*resolution, ImageBufferFormats.BufferRGB888A8);
            //_noiseBase = GorgonLibrary.Graphics.Image.FromStream("perlinnoisebase" + RandomString.Generate(6), new MemoryStream(noisyColors), resolution*resolution*4, resolution,resolution,ImageBufferFormats.BufferRGB888A8 );
            _noiseBase = new Image("perlinnoisebase" + RandomString.Generate(6), resolution,
                                   resolution, ImageBufferFormats.BufferRGB888A8, true);
            Image.ImageLockBox imagelock =
                _noiseBase.GetImageData();

            imagelock.Lock(true);
            imagelock.Write(noisyColors);
            imagelock.Unlock();
            imagelock.Dispose();
            //noiseImage.SetData(noisyColors);
            //return noiseImage;
        }
    }
}
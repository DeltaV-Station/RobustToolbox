using System.Collections.Generic;
using Robust.Client.Interfaces.Graphics;
using Robust.Client.Interfaces.Graphics.ClientEye;
using Robust.Shared.Maths;

namespace Robust.Client.Graphics.Clyde
{
    internal sealed partial class Clyde
    {
        private readonly List<Viewport> _viewports = new List<Viewport>();

        private Viewport CreateViewport(Vector2i size, string? name = null)
        {
            var viewport = new Viewport(name);
            viewport.Size = size;
            viewport.RenderTarget = CreateRenderTarget(size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8, true),
                name: $"{name}-MainRenderTarget");

            RegenLightRts(viewport);

            _viewports.Add(viewport);

            return viewport;
        }

        IClydeViewport IClyde.CreateViewport(Vector2i size, string? name)
        {
            return CreateViewport(size, name);
        }

        private sealed class Viewport : IClydeViewport
        {
            // Primary render target.
            public RenderTarget RenderTarget = default!;

            // Various render targets used in the light rendering process.

            // Lighting is drawn into this. This then gets sampled later while rendering world-space stuff.
            public RenderTarget LightRenderTarget = default!;

            // Unused, to be removed.
            public RenderTarget WallMaskRenderTarget = default!;

            // Two render targets used to apply gaussian blur to the _lightRenderTarget so it bleeds "into" walls.
            // We need two of them because efficient blur works in two stages and also we're doing multiple iterations.
            public RenderTarget WallBleedIntermediateRenderTarget1 = default!;
            public RenderTarget WallBleedIntermediateRenderTarget2 = default!;

            public string? Name { get; }

            public Viewport(string? name)
            {
                Name = name;
            }


            public Vector2i Size { get; set; }

            public void Dispose()
            {
            }


            IRenderTarget IClydeViewport.RenderTarget => RenderTarget;
            public IEye? Eye { get; set; }

            /*public void Resize(Vector2i newSize)
            {
                Size = newSize;
            }*/
        }
    }
}

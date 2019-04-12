using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.Graphics.ClientEye;
using Robust.Client.Interfaces.Map;
using Robust.Client.Interfaces.ResourceManagement;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using Robust.Shared.GameObjects.Components.Renderable;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using Image = SixLabors.ImageSharp.Image;

namespace Robust.Client.Map
{
    internal sealed class ClydeTileDefinitionManager : TileDefinitionManager, IClydeTileDefinitionManager
    {
        [Dependency] private IResourceCache _resourceCache;

        public Texture TileTextureAtlas { get; private set; }

        private readonly Dictionary<ushort, UIBox2> _tileRegions = new Dictionary<ushort, UIBox2>();

        public UIBox2? TileAtlasRegion(Tile tile)
        {
            if (_tileRegions.TryGetValue(tile.TileId, out var region))
            {
                return region;
            }

            return null;
        }

        public override void Initialize()
        {
            base.Initialize();

            _genTextureAtlas();
        }

        private void _genTextureAtlas()
        {
            var defList = TileDefs.Where(t => !string.IsNullOrEmpty(t.SpriteName)).ToList();
            const int tileSize = EyeManager.PIXELSPERMETER;

            var dimensionX = (int) Math.Ceiling(Math.Sqrt(defList.Count));
            var dimensionY = (int) Math.Ceiling((float) defList.Count / dimensionX);

            var sheet = new Image<Rgba32>(dimensionX * tileSize, dimensionY * tileSize);

            for (var i = 0; i < defList.Count; i++)
            {
                var def = defList[i];
                var column = i % dimensionX;
                var row = i / dimensionX;

                Image<Rgba32> image;
                using (var stream = _resourceCache.ContentFileRead($"/Textures/Tiles/{def.SpriteName}.png"))
                {
                    image = Image.Load(stream);
                }

                if (image.Width != tileSize || image.Height != tileSize)
                {
                    throw new NotImplementedException("Unable to use tiles with a dimension other than 32x32.");
                }

                var point = new Point(column * tileSize, row * tileSize);

                sheet.Mutate(x => x.DrawImage(image, point,
                    PixelColorBlendingMode.Overlay, 1));

                _tileRegions.Add(def.TileId,
                    UIBox2.FromDimensions(
                        point.X / (float) sheet.Width, point.Y / (float) sheet.Height,
                        tileSize / (float) sheet.Width, tileSize / (float) sheet.Height));
            }

            TileTextureAtlas = Texture.LoadFromImage(sheet, "Tile Atlas");
        }
    }
}

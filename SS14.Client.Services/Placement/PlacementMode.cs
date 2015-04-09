﻿using SS14.Client.Graphics.CluwneLib.Sprite;
using SS14.Client.Interfaces.Map;
using SS14.Shared.Maths;

namespace SS14.Client.Services.Placement
{
    public class PlacementMode
    {
        public readonly PlacementManager pManager;

        public TileRef currentTile;
        public Vector2 mouseScreen;
        public Vector2 mouseWorld;
		public CluwneSprite spriteToDraw;

        public PlacementMode(PlacementManager pMan)
        {
            pManager = pMan;
        }

        public virtual string ModeName
        {
            get { return GetType().Name; }
        }

        public virtual bool Update(Vector2 mouseScreen, IMapManager currentMap) //Return valid position?
        {
            return false;
        }

        public virtual void Render()
        {
        }

		public CluwneSprite GetDirectionalSprite(CluwneSprite baseSprite)
        {
			CluwneSprite spriteToUse = baseSprite;

            if (baseSprite == null) return null;

            string dirName = (baseSprite.Name + "_" + pManager.Direction.ToString()).ToLowerInvariant();
            if (pManager.ResourceManager.SpriteExists(dirName))
                spriteToUse = pManager.ResourceManager.GetSprite(dirName);

            return spriteToUse;
        }
    }
}
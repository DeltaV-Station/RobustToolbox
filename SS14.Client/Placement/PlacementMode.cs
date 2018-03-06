﻿using SS14.Client.Graphics;
using SS14.Client.Graphics.Sprites;
using SS14.Shared.Interfaces.GameObjects.Components;
using SS14.Shared.Map;
using SS14.Shared.Maths;

namespace SS14.Client.Placement
{
    public class PlacementMode
    {
        public readonly PlacementManager pManager;
        public TileRef CurrentTile { get; set; }
        public ScreenCoordinates MouseScreen { get ; set; }
        public LocalCoordinates MouseCoords { get; set; }
        public Sprite SpriteToDraw { get; set; }
        public Color ValidPlaceColor { get; set; } = new Color(34, 139, 34); //Default valid color is green
        public Color InvalidPlaceColor { get; set; } = new Color(34, 34, 139); //Default invalid placement is red
        
        public virtual bool rangerequired => false;

        public PlacementMode(PlacementManager pMan)
        {
            pManager = pMan;
        }

        public virtual string ModeName
        {
            get { return GetType().Name; }
        }


        public virtual bool Update(ScreenCoordinates mouseScreen)
        {
            return false;
        }

        public virtual void Render()
        {
            if(SpriteToDraw == null)
            {
                SetSprite();
            }

            var bounds = SpriteToDraw.LocalBounds;
            SpriteToDraw.Color = pManager.ValidPosition ? ValidPlaceColor : InvalidPlaceColor;
            SpriteToDraw.Position = new Vector2(MouseScreen.X - (bounds.Width / 2f),
                                                MouseScreen.Y - (bounds.Height / 2f));
            //Centering the sprite on the cursor.
            SpriteToDraw.Draw();
        }

        public void SetSprite()
        {
            SpriteToDraw = GetDirectionalSprite(pManager.CurrentBaseSpriteKey);
            SpriteToDraw = new Sprite(SpriteToDraw);
        }

        public Sprite GetSprite(string key)
        {
            if (key == null || !pManager.ResourceCache.SpriteExists(key))
            {
                return pManager.ResourceCache.DefaultSprite();
            }
            else
            {
                return pManager.ResourceCache.GetSprite(key);
            }
        }

        public Sprite GetDirectionalSprite(string baseSprite)
        {
            if (baseSprite == null) pManager.ResourceCache.DefaultSprite();

            var directionalspritename = (baseSprite + "_" + pManager.Direction.ToString()).ToLowerInvariant();

            if(pManager.ResourceCache.SpriteExists(directionalspritename))
            {
                return pManager.ResourceCache.GetSprite(directionalspritename);
            }
            else
            {
                return GetSprite(baseSprite);
            }
        }

        public bool RangeCheck()
        {
            if (!rangerequired)
                return true;
            var range = pManager.CurrentPermission.Range;
            if (range > 0 && !pManager.PlayerManager.LocalPlayer.ControlledEntity.GetComponent<ITransformComponent>().LocalPosition.InRange(MouseCoords, range))
                    return false;
            return true;
        }

        public bool CheckCollision()
        {
            var drawsprite = GetSprite(pManager.CurrentBaseSpriteKey);
            var bounds = drawsprite.LocalBounds;
            var spriteSize = CluwneLib.PixelToTile(new Vector2(bounds.Width, bounds.Height));
            var spriteRectWorld = Box2.FromDimensions(MouseCoords.X - (spriteSize.X / 2f),
                                                 MouseCoords.Y - (spriteSize.Y / 2f),
                                                 spriteSize.X, spriteSize.Y);
            if (pManager.CollisionManager.IsColliding(spriteRectWorld))
                return false;
            return true;
        }
    }
}

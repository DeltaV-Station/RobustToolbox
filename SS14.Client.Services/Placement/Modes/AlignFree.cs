﻿using SS14.Shared.Maths;
using SS14.Client.ClientWindow;
using SS14.Client.Interfaces.Map;
using System.Drawing;
using SS14.Client.Graphics.CluwneLib;

namespace SS14.Client.Services.Placement.Modes
{
    public class AlignFree : PlacementMode
    {
        public AlignFree(PlacementManager pMan)
            : base(pMan)
        {
        }

        public override bool Update(Vector2 mouseS, IMapManager currentMap)
        {
            if (currentMap == null) return false;

            spriteToDraw = GetDirectionalSprite(pManager.CurrentBaseSprite);

            mouseScreen = mouseS;
            mouseWorld = new Vector2(mouseScreen.X + ClientWindowData.Singleton.ScreenOrigin.X,
                                      mouseScreen.Y + ClientWindowData.Singleton.ScreenOrigin.Y);
            currentTile = currentMap.GetFloorAt(mouseWorld);

            return true;
        }

        public override void Render()
        {
            if (spriteToDraw != null)
            {
                spriteToDraw.Color = pManager.ValidPosition ? CluwneLib.SystemColorToSFML(Color.ForestGreen) : CluwneLib.SystemColorToSFML(Color.IndianRed);
                spriteToDraw.Position = new Vector2(mouseScreen.X - (spriteToDraw.Width/2f),
                                                     mouseScreen.Y - (spriteToDraw.Height/2f));
                //Centering the sprite on the cursor.
                spriteToDraw.Draw();
                spriteToDraw.Color = CluwneLib.SystemColorToSFML(Color.White);
            }
        }
    }
}
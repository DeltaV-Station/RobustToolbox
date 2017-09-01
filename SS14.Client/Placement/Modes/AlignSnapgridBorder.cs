﻿using OpenTK;
using SFML.Graphics;
using SS14.Client.GameObjects;
using SS14.Client.Graphics;
using SS14.Shared.Interfaces.Map;
using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.GameObjects.Components;
using SS14.Shared.Utility;
using SS14.Shared.Maths;
using System;
using OpenTK.Graphics;
using SS14.Shared.Map;

namespace SS14.Client.Placement.Modes
{
    public class SnapgridBorder : PlacementMode
    {
        bool ongrid;
        float snapsize;

        public SnapgridBorder(PlacementManager pMan) : base(pMan)
        {
        }

        public override void Render()
        {
            base.Render();
            if (ongrid)
            {
                var position = CluwneLib.ScreenToWorld(new Vector2i(0,0));  //Find world coordinates closest to screen origin
                var gridstart = CluwneLib.WorldToScreen(new Vector2( //Find snap grid closest to screen origin and convert back to screen coords
                (float)Math.Round((position.X / (double)snapsize), MidpointRounding.AwayFromZero) * snapsize,
                (float)Math.Round((position.Y / (double)snapsize), MidpointRounding.AwayFromZero) * snapsize));
                for (float a = gridstart.X; a < CluwneLib.ScreenViewportSize.X; a += snapsize * 32) //Iterate through screen creating gridlines
                {
                    CluwneLib.drawLine(a, 0, CluwneLib.ScreenViewportSize.Y, 90, 0.5f, Color4.Blue);
                }
                for (float a = gridstart.Y; a < CluwneLib.ScreenViewportSize.Y; a += snapsize * 32)
                {
                    CluwneLib.drawLine(0, a, CluwneLib.ScreenViewportSize.X, 0, 0.5f, Color4.Blue);
                }
            }
        }

        public override bool Update(ScreenCoordinates mouseS)
        {
            if (mouseS.MapID == Coordinates.NULLSPACE) return false;

            mouseScreen = mouseS;
            snapsize = mouseCoords.Grid.SnapSize; //Find snap size.
            
            var mouselocal = new Vector2( //Round local coordinates onto the snap grid
                (float)Math.Round((mouseCoords.X / (double)snapsize), MidpointRounding.AwayFromZero) * snapsize,
                (float)Math.Round((mouseCoords.Y / (double)snapsize), MidpointRounding.AwayFromZero) * snapsize);
            
            //Convert back to original world and screen coordinates after applying offset
            mouseCoords.Position = mouselocal + new Vector2(pManager.CurrentPrototype.PlacementOffset.X, pManager.CurrentPrototype.PlacementOffset.Y);
            mouseScreen = CluwneLib.WorldToScreen(mouseCoords);

            if (!RangeCheck())
                return false;

            return true;
        }
    }
}

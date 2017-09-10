﻿using OpenTK;
using SFML.Graphics;
using SFML.System;
using SS14.Client.GameObjects;
using SS14.Client.Graphics;
using SS14.Shared.Interfaces.Map;
using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.GameObjects.Components;
using SS14.Shared.Maths;
using SS14.Shared.Utility;
using Vector2i = SS14.Shared.Maths.Vector2i;
using SS14.Shared.IoC;
using SS14.Client.Interfaces.GameObjects;
using SS14.Shared.Map;
using Vector2 = SS14.Shared.Maths.Vector2;

namespace SS14.Client.Placement.Modes
{
    public class AlignTileEmpty : PlacementMode
    {
        public AlignTileEmpty(PlacementManager pMan) : base(pMan)
        {
        }

        public override bool Update(ScreenCoordinates mouseS)
        {
            if (mouseS.MapID == MapManager.NULLSPACE) return false;

            mouseScreen = mouseS;
            mouseCoords = CluwneLib.ScreenToCoordinates(mouseScreen);

            currentTile = mouseCoords.Grid.GetTile(mouseCoords);
            var tilesize = mouseCoords.Grid.TileSize;

            if (!RangeCheck())
                return false;

            var entitymanager = IoCManager.Resolve<IClientEntityManager>();
            var failtoplace = !entitymanager.AnyEntitiesIntersecting(new Box2(new Vector2(currentTile.X, currentTile.Y), new Vector2(currentTile.X + 0.99f, currentTile.Y + 0.99f)));

            if (pManager.CurrentPermission.IsTile)
            {
                mouseCoords = new LocalCoordinates(currentTile.X + tilesize/2,
                                                  currentTile.Y + tilesize/2,
                                                  mouseCoords.Grid);
                mouseScreen = CluwneLib.WorldToScreen(mouseCoords);
            }
            else
            {
                mouseCoords = new LocalCoordinates(currentTile.X + tilesize/2 + pManager.CurrentPrototype.PlacementOffset.X,
                                                  currentTile.Y + tilesize/2 + pManager.CurrentPrototype.PlacementOffset.Y,
                                                  mouseCoords.Grid);
                mouseScreen = CluwneLib.WorldToScreen(mouseCoords);
            }
            return failtoplace;
        }
    }
}

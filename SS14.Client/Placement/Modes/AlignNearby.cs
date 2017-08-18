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

namespace SS14.Client.Placement.Modes
{
    public class AlignNearby : PlacementMode
    {
        public AlignNearby(PlacementManager pMan) : base(pMan)
        {
        }

        public override bool RangeRequired()
        {
            return true;
        }

        public override bool Update(Vector2i mouseS, IMapManager currentMap)
        {
            if (currentMap == null) return false;

            mouseScreen = mouseS;
            mouseWorld = CluwneLib.ScreenToWorld(mouseScreen);

            if (pManager.CurrentPermission.IsTile)
                return false;

            if (CheckCollision())
                return false;

            if (RangeRequired() && !RangeCheck())
                return false;

            currentTile = currentMap.GetDefaultGrid().GetTile(mouseWorld);

            return true;
        }
    }
}

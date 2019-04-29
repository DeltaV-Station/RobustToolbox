﻿using Robust.Shared.Map;

namespace Robust.Client.Placement.Modes
{
    public class PlaceNearby : PlacementMode
    {
        public PlaceNearby(PlacementManager pMan) : base(pMan) { }

        public override bool RangeRequired => true;

        public override void AlignPlacementMode(ScreenCoordinates mouseScreen)
        {
            MouseCoords = ScreenToPlayerGrid(mouseScreen);
            CurrentTile = pManager.MapManager.GetGrid(MouseCoords.GridID).GetTileRef(MouseCoords);
        }

        public override bool IsValidPosition(GridCoordinates position)
        {
            if (pManager.CurrentPermission.IsTile)
            {
                return false;
            }
            else if (!RangeCheck(position))
            {
                return false;
            }

            return true;
        }
    }
}

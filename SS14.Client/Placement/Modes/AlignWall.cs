﻿using SFML.Graphics;
using SFML.System;
using SS14.Client.GameObjects;
using SS14.Client.Graphics;
using SS14.Client.Interfaces.Map;
using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.GameObjects.Components;
using SS14.Shared.Maths;
using System.Collections.Generic;
using System.Linq;
using SS14.Shared.Utility;
using Vector2i = SFML.System.Vector2i;

namespace SS14.Client.Placement.Modes
{
    public class AlignWall : PlacementMode
    {
        public AlignWall(PlacementManager pMan)
            : base(pMan)
        {
        }

        public override bool Update(Vector2i mouseS, IMapManager currentMap)
        {
            if (currentMap == null) return false;

            spriteToDraw = GetDirectionalSprite(pManager.CurrentBaseSpriteKey);

            mouseScreen = mouseS;
            mouseWorld = CluwneLib.ScreenToWorld(mouseScreen);

            var bounds = spriteToDraw.GetLocalBounds();
            var spriteSize = CluwneLib.PixelToTile(new Vector2f(bounds.Width, bounds.Height));

            if (pManager.CurrentPermission.IsTile)
                return false;

            currentTile = currentMap.GetTileRef(mouseWorld);

            if (!currentTile.Tile.TileDef.IsWall)
                return false;

            var rangeSquared = pManager.CurrentPermission.Range * pManager.CurrentPermission.Range;
            if (rangeSquared > 0)
                if (
                    (pManager.PlayerManager.ControlledEntity.GetComponent<ITransformComponent>()
                         .Position - mouseWorld.Convert()).LengthSquared > rangeSquared)
                    return false;

            var nodes = new List<Vector2f>();

            if (pManager.CurrentPrototype.MountingPoints != null)
            {
                nodes.AddRange(
                    pManager.CurrentPrototype.MountingPoints.Select(
                        current => new Vector2f(mouseWorld.X, currentTile.Y + current)));
            }
            else
            {
                nodes.Add(new Vector2f(mouseWorld.X, currentTile.Y + 0.5f));
                nodes.Add(new Vector2f(mouseWorld.X, currentTile.Y + 1.0f));
                nodes.Add(new Vector2f(mouseWorld.X, currentTile.Y + 1.5f));
            }

            Vector2f closestNode = (from Vector2f node in nodes
                                    orderby (node - mouseWorld).LengthSquared() ascending
                                    select node).First();

            mouseWorld = closestNode + new Vector2f(pManager.CurrentPrototype.PlacementOffset.X,
                                                    pManager.CurrentPrototype.PlacementOffset.Y);
            mouseScreen = CluwneLib.WorldToScreen(mouseWorld).Round();

            var range = pManager.CurrentPermission.Range;
            if (range > 0)
                if (
                    (pManager.PlayerManager.ControlledEntity.GetComponent<ITransformComponent>()
                         .Position - mouseWorld.Convert()).LengthSquared > range * range)
                    return false;

            return true;
        }

        public override void Render()
        {
            if (spriteToDraw != null)
            {
                var bounds = spriteToDraw.GetLocalBounds();
                spriteToDraw.Color = pManager.ValidPosition ? new SFML.Graphics.Color(34, 139, 34) : new SFML.Graphics.Color(205, 92, 92);
                spriteToDraw.Position = new Vector2f(mouseScreen.X - (bounds.Width/2f),
                                                     mouseScreen.Y - (bounds.Height/2f));
                //Centering the sprite on the cursor.
                spriteToDraw.Draw();
                spriteToDraw.Color = Color.White;
            }
        }
    }
}

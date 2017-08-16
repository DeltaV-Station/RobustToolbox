﻿using SFML.Graphics;
using SFML.System;
using SS14.Client.GameObjects;
using SS14.Client.Graphics;
using SS14.Client.Helpers;
using SS14.Client.Interfaces.GameObjects;
using SS14.Client.Interfaces.Map;
using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.Interfaces.GameObjects.Components;
using SS14.Shared.IoC;
using SS14.Shared.Maths;
using System.Collections.Generic;
using System.Linq;
using SS14.Shared.Utility;
using Vector2i = SFML.System.Vector2i;

namespace SS14.Client.Placement.Modes
{
    public class AlignSimilar : PlacementMode
    {
        private const uint snapToRange = 50;

        public AlignSimilar(PlacementManager pMan) : base(pMan)
        {
        }

        public override bool Update(Vector2i mouseS, IMapManager currentMap)
        {
            if (currentMap == null) return false;

            spriteToDraw = GetDirectionalSprite(pManager.CurrentBaseSpriteKey);
            var spriteBounds = spriteToDraw.GetLocalBounds();

            mouseScreen = mouseS;
            mouseWorld = CluwneLib.ScreenToWorld(mouseScreen);

            if (pManager.CurrentPermission.IsTile)
                return false;

            currentTile = currentMap.GetTileRef(mouseWorld);

            //Align to similar if nearby found else free
            if (currentTile.Tile.TileDef.IsWall)
                return false; //HANDLE CURSOR OUTSIDE MAP

            var rangeSquared = pManager.CurrentPermission.Range * pManager.CurrentPermission.Range;
            if (rangeSquared > 0)
                if (
                    (pManager.PlayerManager.ControlledEntity.GetComponent<ITransformComponent>()
                         .Position - mouseWorld.Convert()).LengthSquared > rangeSquared) return false;

            var manager = IoCManager.Resolve<IClientEntityManager>();

            IOrderedEnumerable<IEntity> snapToEntities =
                from IEntity entity in manager.GetEntitiesInRange(mouseWorld, snapToRange)
                where entity.Prototype == pManager.CurrentPrototype
                orderby
                    (entity.GetComponent<ITransformComponent>(
                        ).Position - mouseWorld.Convert()).LengthSquared
                    ascending
                select entity;

            if (snapToEntities.Any())
            {
                IEntity closestEntity = snapToEntities.First();
                if (closestEntity.TryGetComponent<ISpriteRenderableComponent>(out var component))
                {
                    var closestSprite = component.GetCurrentSprite();
                    var closestBounds = closestSprite.GetLocalBounds();

                    var closestRect =
                        new FloatRect(
                            closestEntity.GetComponent<ITransformComponent>().Position.X - closestBounds.Width / 2f,
                            closestEntity.GetComponent<ITransformComponent>().Position.Y - closestBounds.Height / 2f,
                            closestBounds.Width, closestBounds.Height);

                    var sides = new List<Vector2>
                    {
                        new Vector2(closestRect.Left + (closestRect.Width / 2f), closestRect.Top - closestBounds.Height / 2f),
                        new Vector2(closestRect.Left + (closestRect.Width / 2f), closestRect.Bottom() + closestBounds.Height / 2f),
                        new Vector2(closestRect.Left - closestBounds.Width / 2f, closestRect.Top + (closestRect.Height / 2f)),
                        new Vector2(closestRect.Right() + closestBounds.Width / 2f, closestRect.Top + (closestRect.Height / 2f))
                    };

                    Vector2 closestSide =
                        (from Vector2 side in sides orderby (side - mouseWorld).LengthSquared() ascending select side).First();

                    mouseWorld = closestSide;
                    mouseScreen = CluwneLib.WorldToScreen(mouseWorld).Round();
                }
            }

            FloatRect spriteRectWorld = new FloatRect(mouseWorld.X - (spriteBounds.Width / 2f), mouseWorld.Y - (spriteBounds.Height / 2f),
                                             spriteBounds.Width, spriteBounds.Height);
            if (pManager.CollisionManager.IsColliding(spriteRectWorld)) return false;
            return true;
        }
    }
}

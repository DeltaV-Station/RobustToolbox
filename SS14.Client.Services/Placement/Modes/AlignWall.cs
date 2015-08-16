using SS14.Shared.Maths;
using SS14.Client.GameObjects;
using SS14.Client.Interfaces.Map;
using SS14.Shared.GO;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using SS14.Client.Graphics;
using SS14.Shared.IoC;

namespace SS14.Client.Services.Placement.Modes
{
    public class AlignWall : PlacementMode
    {
        public AlignWall(PlacementManager pMan)
            : base(pMan)
        {
        }

        public override bool Update(Vector2 mouseS, IMapManager currentMap)
        {
            if (currentMap == null) return false;

            spriteToDraw = GetDirectionalSprite(pManager.CurrentBaseSprite);

            int tileSize = IoCManager.Resolve<IMapManager>().TileSize;
            mouseScreen = mouseS;
            mouseWorld = mouseScreen / tileSize;

            var spriteSize = spriteToDraw.Size / tileSize;
            var spriteRectWorld = new RectangleF(mouseWorld.X - (spriteSize.X / 2f),
                                                 mouseWorld.Y - (spriteSize.Y / 2f),
                                                 spriteSize.X, spriteSize.Y);

            if (pManager.CurrentPermission.IsTile)
                return false;

            //CollisionManager collisionMgr = (CollisionManager)ServiceManager.Singleton.GetService(ClientServiceType.CollisionManager);
            //if (collisionMgr.IsColliding(spriteRectWorld, true)) validPosition = false;

            currentTile = currentMap.GetTileRef(mouseWorld);

            if (!currentTile.Tile.TileDef.IsWall)
                return false;

            if (pManager.CurrentPermission.Range > 0)
                if (
                    (pManager.PlayerManager.ControlledEntity.GetComponent<TransformComponent>(ComponentFamily.Transform)
                         .Position - mouseWorld).Length > pManager.CurrentPermission.Range)
                    return false;

            var nodes = new List<Vector2>();

            if (pManager.CurrentTemplate.MountingPoints != null)
            {
                nodes.AddRange(
                    pManager.CurrentTemplate.MountingPoints.Select(
                        current => new Vector2(mouseWorld.X, currentTile.Y + current)));
            }
            else
            {
                nodes.Add(new Vector2(mouseWorld.X, currentTile.Y + 0.5f));
                nodes.Add(new Vector2(mouseWorld.X, currentTile.Y + 1.0f));
                nodes.Add(new Vector2(mouseWorld.X, currentTile.Y + 1.5f));
            }

            Vector2 closestNode = (from Vector2 node in nodes
                                    orderby (node - mouseWorld).Length ascending
                                    select node).First();

            mouseWorld = Vector2.Add(closestNode,
                                      new Vector2(pManager.CurrentTemplate.PlacementOffset.Key,
                                                   pManager.CurrentTemplate.PlacementOffset.Value));
            mouseScreen = mouseWorld * tileSize;

            if (pManager.CurrentPermission.Range > 0)
                if (
                    (pManager.PlayerManager.ControlledEntity.GetComponent<TransformComponent>(ComponentFamily.Transform)
                         .Position - mouseWorld).Length > pManager.CurrentPermission.Range)
                    return false;

            return true;
        }

        public override void Render()
        {
            if (spriteToDraw != null)
            {
                spriteToDraw.Color = pManager.ValidPosition ? Color.ForestGreen.ToSFMLColor(): Color.IndianRed.ToSFMLColor();
                spriteToDraw.Position = new Vector2(mouseScreen.X - (spriteToDraw.Width/2f),
                                                     mouseScreen.Y - (spriteToDraw.Height/2f));
                //Centering the sprite on the cursor.
                spriteToDraw.Draw();
                spriteToDraw.Color = Color.White.ToSFMLColor();
            }
        }
    }
}
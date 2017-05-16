﻿using SFML.Graphics;
using SS14.Client.Interfaces.GOC;
using SS14.Client.Interfaces.Placement;
using SS14.Client.Interfaces.Resource;
using SS14.Client.Interfaces.UserInterface;
using SS14.Client.Services.Helpers;
using SS14.Shared.GameObjects;
using SS14.Shared.IoC;

namespace SS14.Client.Services.UserInterface
{
    public class DragDropInfo : IDragDropInfo
    {
        #region IDragDropInfo Members

        public Entity DragEntity { get; private set; }
        public Sprite DragSprite { get; private set; }
        public bool IsEntity { get; private set; }

        public bool IsActive
        {
            get { return Active(); }
        }

        public void Reset()
        {
            DragEntity = null;
            DragSprite = null;
            IsEntity = true;
        }

        public void StartDrag(Entity entity)
        {
            Reset();

            IoCManager.Resolve<IPlacementManager>().Clear();

            DragEntity = entity;
            DragSprite = Utilities.GetIconSprite(entity);
            IsEntity = true;
        }

        #endregion

        public bool Active()
        {
            return DragEntity != null;
        }
    }
}

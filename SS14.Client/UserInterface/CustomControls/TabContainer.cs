﻿using SS14.Client.Graphics.Input;
using SS14.Client.Graphics.Sprites;
using SS14.Client.Interfaces.Resource;
using SS14.Client.UserInterface.Components;
using SS14.Client.UserInterface.Controls;
using Vector2i = SS14.Shared.Maths.Vector2i;

namespace SS14.Client.UserInterface.CustomControls
{
    internal class TabContainer : ScrollableContainer
    {
        public string tabName = "";
        public Sprite tabSprite = null;

        public TabContainer(string uniqueName, Vector2i size, IResourceCache resourceCache)
            : base(uniqueName, size)
        {
            DrawBorder = false;
        }

        public string tabSpriteName
        {
            set { tabSprite = _resourceCache.GetSprite(value); }
        }

        public virtual void Activated() //Called when tab is selected.
        {
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
        }

        public override void Draw()
        {
            base.Draw();
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        public override bool MouseDown(MouseButtonEventArgs e)
        {
            return base.MouseDown(e);
        }

        public override bool MouseUp(MouseButtonEventArgs e)
        {
            return base.MouseUp(e);
        }

        public override void MouseMove(MouseMoveEventArgs e)
        {
            base.MouseMove(e);
        }
    }
}

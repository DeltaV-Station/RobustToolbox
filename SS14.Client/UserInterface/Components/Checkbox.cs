﻿using SS14.Client.Graphics.Input;
using SS14.Client.Graphics.Sprites;
using SS14.Client.Interfaces.Resource;
using SS14.Shared.Maths;
using System;
using Vector2i = SS14.Shared.Maths.Vector2i;

namespace SS14.Client.UserInterface.Components
{
    internal class Checkbox : GuiComponent
    {
        #region Delegates

        public delegate void CheckboxChangedHandler(Boolean newValue, Checkbox sender);

        #endregion Delegates

        private readonly IResourceCache _resourceCache;

        private Sprite checkbox;
        private Sprite checkboxCheck;

        private bool value;

        public Checkbox(IResourceCache resourceCache)
        {
            _resourceCache = resourceCache;
            checkbox = _resourceCache.GetSprite("checkbox0");
            checkboxCheck = _resourceCache.GetSprite("checkbox1");

            ClientArea = Box2i.FromDimensions(Position,
                new Vector2i((int)checkbox.LocalBounds.Width, (int)checkbox.LocalBounds.Height));
            Update(0);
        }

        public bool Value
        {
            get { return value; }
            set
            {
                if (ValueChanged != null) ValueChanged(value, this);
                this.value = value;
            }
        }

        public event CheckboxChangedHandler ValueChanged;

        public override void Update(float frameTime)
        {
            checkbox.Position = Position;
        }

        public override void Render()
        {
            checkbox.Draw();
            if (Value) checkboxCheck.Draw();
        }

        public override void Dispose()
        {
            checkbox = null;
            checkboxCheck = null;
            ValueChanged = null;
            base.Dispose();
            GC.SuppressFinalize(this);
        }

        public override bool MouseDown(MouseButtonEventArgs e)
        {
            if (ClientArea.Contains(new Vector2i(e.X, e.Y)))
            {
                Value = !Value;
                return true;
            }
            return false;
        }

        public override bool MouseUp(MouseButtonEventArgs e)
        {
            return false;
        }
    }
}

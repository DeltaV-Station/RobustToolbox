﻿using System;
using SS14.Client.Graphics;

namespace SS14.Client.UserInterface.Controls
{
    [ControlWrap("TextureRect")]
    public class TextureRect : Control
    {
        public TextureRect() : base()
        {
        }
        public TextureRect(string name) : base(name)
        {
        }

        #if GODOT
        public TextureRect(Godot.TextureRect button) : base(button)
        {
        }
        #endif

        public Texture Texture
        {
            #if GODOT
            // TODO: Maybe store the texture passed in in case it's like a TextureResource or whatever.
            get => new GodotTextureSource(SceneControl.Texture);
            set => SceneControl.Texture = value?.GodotTexture;
            #else
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
            #endif
        }

        #if GODOT
        new private Godot.TextureRect SceneControl;

        private protected override Godot.Control SpawnSceneControl()
        {
            return new Godot.TextureRect();
        }

        private protected override void SetSceneControl(Godot.Control control)
        {
            base.SetSceneControl(control);
            SceneControl = (Godot.TextureRect)control;
        }
        #endif
    }
}

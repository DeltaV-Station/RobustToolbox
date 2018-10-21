﻿using System;
using SS14.Client.Graphics;

namespace SS14.Client.UserInterface.Controls
{
    [ControlWrap(typeof(Godot.TextureRect))]
    public class TextureRect : Control
    {
        public TextureRect() : base()
        {
        }

        public TextureRect(string name) : base(name)
        {
        }

        public TextureRect(Godot.TextureRect button) : base(button)
        {
        }

        public Texture Texture
        {
            // TODO: Maybe store the texture passed in in case it's like a TextureResource or whatever.
            get => GameController.OnGodot ? (Texture) new GodotTextureSource(SceneControl.Texture) : new BlankTexture();
            set
            {
                if (GameController.OnGodot)
                {
                    SceneControl.Texture = value?.GodotTexture;
                }
            }
        }

        new private Godot.TextureRect SceneControl;

        private protected override Godot.Control SpawnSceneControl()
        {
            return new Godot.TextureRect();
        }

        private protected override void SetSceneControl(Godot.Control control)
        {
            base.SetSceneControl(control);
            SceneControl = (Godot.TextureRect) control;
        }
    }
}

﻿namespace SS14.Client.UserInterface.Controls
{
    [ControlWrap("HBoxContainer")]
    public class HBoxContainer : BoxContainer
    {
        public HBoxContainer() : base() { }
        public HBoxContainer(string name) : base(name) { }
        #if GODOT
        internal HBoxContainer(Godot.HBoxContainer sceneControl) : base(sceneControl) { }

        private protected override Godot.Control SpawnSceneControl()
        {
            return new Godot.HBoxContainer();
        }
        #endif
    }
}

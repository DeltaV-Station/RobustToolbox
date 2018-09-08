using SS14.Client.Graphics.Drawing;

namespace SS14.Client.UserInterface.Controls
{
    [ControlWrap(typeof(Godot.PanelContainer))]
    public class PanelContainer : Control
    {
        public PanelContainer()
        {
        }

        public PanelContainer(string name) : base(name)
        {
        }

        public PanelContainer(Godot.PanelContainer container) : base(container)
        {
        }

        private StyleBox _panelOverride;

        public StyleBox PanelOverride
        {
            get => _panelOverride ?? GetStyleBoxOverride("panel");
            set => SetStyleBoxOverride("panel", _panelOverride = value);
        }

        protected override Godot.Control SpawnSceneControl()
        {
            return new Godot.PanelContainer();
        }
    }
}

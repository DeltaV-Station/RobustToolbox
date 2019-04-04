﻿using SS14.Client.Graphics.Drawing;
using SS14.Client.Graphics.Overlays;
using SS14.Client.Interfaces.Console;
using SS14.Client.Interfaces.Graphics.Overlays;
using SS14.Shared.IoC;
using SS14.Shared.Maths;

namespace SS14.Client.GameStates
{
    internal class NetGraphOverlay : Overlay
    {
        public override OverlaySpace Space => OverlaySpace.ScreenSpace;

        public NetGraphOverlay() : base(nameof(NetGraphOverlay))
        {
            IoCManager.InjectDependencies(this);
        }

        protected override void Draw(DrawingHandle handle)
        {
            handle.DrawLine(new Vector2(50,50), new Vector2(100,100), Color.Green);
        }

        private class NetShowGraph : IConsoleCommand
        {
            public string Command => "net_graph";
            public string Help => "net_graph <0|1>";
            public string Description => "Toggles the net statistics pannel.";

            public bool Execute(IDebugConsole console, params string[] args)
            {
                if (args.Length != 1)
                {
                    console.AddLine("Invalid argument amount. Expected 2 arguments.", Color.Red);
                    return false;
                }

                if (!byte.TryParse(args[0], out var iValue))
                {
                    console.AddLine("Invalid argument: Needs to be 0 or 1.");
                    return false;
                }

                var bValue = iValue > 0;
                var overlayMan = IoCManager.Resolve<IOverlayManager>();

                if(bValue && !overlayMan.HasOverlay(nameof(NetGraphOverlay)))
                {
                    overlayMan.AddOverlay(new NetGraphOverlay());
                    console.AddLine("Enabled network overlay.");
                }
                else if(overlayMan.HasOverlay(nameof(NetGraphOverlay)))
                {
                    overlayMan.RemoveOverlay(nameof(NetGraphOverlay));
                    console.AddLine("Disabled network overlay.");
                }
                
                return false;
            }
        } 
    }
}

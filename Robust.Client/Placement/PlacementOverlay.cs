﻿using Robust.Client.Graphics.Drawing;
using Robust.Client.Graphics.Overlays;
using Robust.Shared.Enums;

namespace Robust.Client.Placement
{
    public partial class PlacementManager
    {
        internal class PlacementOverlay : Overlay
        {
            private readonly PlacementManager _manager;
            public override bool AlwaysDirty => true;
            public override OverlaySpace Space => OverlaySpace.WorldSpace;
            public override OverlayPriority Priority => OverlayPriority.P2;

            public PlacementOverlay(PlacementManager manager) : base()
            {
                _manager = manager;
                ZIndex = 100;
            }

            protected override void Draw(DrawingHandleBase handle, OverlaySpace currentSpace)
            {
                _manager.Render((DrawingHandleWorld) handle);
            }
        }
    }
}

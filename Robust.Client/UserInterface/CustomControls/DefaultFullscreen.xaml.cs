using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Robust.Client.UserInterface.CustomControls
{
    /// <summary>
    /// An implementation for any UI that takes up the entire screen
    /// </summary>
    [GenerateTypedNameReferences]
    [Virtual]
    public partial class DefaultFullscreen : BaseFullscreen
    {
        public DefaultFullscreen()
        {
            RobustXamlLoader.Load(this);
            MouseFilter = MouseFilterMode.Stop;

            Contents = ContentsContainer;

            XamlChildren = new SS14ContentCollection(this);
        }
    }
}

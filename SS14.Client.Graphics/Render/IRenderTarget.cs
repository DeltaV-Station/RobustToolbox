﻿using SS14.Shared.Maths;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS14.Client.Graphics.Render
{
    public interface IRenderTarget
    {
        SFML.Graphics.RenderTarget SFMLTarget { get; }
        Vector2u Size { get; }
        uint Width { get; }
        uint Height { get; }

        void Clear(Color color);
        void Memes(IDrawable drawable);
        // This has to have its own name vs overload
        // Because the C# overload detector refuses to resolve anything
        // if a single overload can't potentially be resolved due to unreferenced assemblies
        // Thus, if this were an overload
        // Draw() would be unusable from SS14.Client, as it doesn't have SFML.Graphics.
        // Yes, this is absolutely retarded.
        void DrawSFML(SFML.Graphics.Drawable drawable);
    }
}

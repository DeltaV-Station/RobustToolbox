using System;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Interfaces.ResourceManagement;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameObjects;
using Robust.Shared.GameObjects.Components.Renderable;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Robust.Client.Utility
{
    /// <summary>
    ///     Helper methods for resolving <see cref="SpriteSpecifier"/>s.
    /// </summary>
    public static class SpriteSpecifierExt
    {
        public static Texture GetTexture(this SpriteSpecifier.Texture texSpecifier, IResourceCache cache)
        {
            return cache
                .GetResource<TextureResource>(SharedSpriteComponent.TextureRoot / texSpecifier.TexturePath)
                .Texture;
        }

        public static RSI.State GetState(this SpriteSpecifier.Rsi rsiSpecifier, IResourceCache cache)
        {
            if (cache.TryGetResource<RSIResource>(
                SharedSpriteComponent.TextureRoot / rsiSpecifier.RsiPath,
                out var theRsi))
            {
                if (theRsi.RSI.TryGetState(rsiSpecifier.RsiState, out var state))
                {
                    return state;
                }
            }

            Logger.Error("Failed to load RSI {0}", rsiSpecifier.RsiPath);
            return SpriteComponent.GetFallbackState(cache);
        }

        public static Texture Frame0(this SpriteSpecifier specifier)
        {
            return specifier.RsiStateLike().Default;
        }

        public static IDirectionalTextureProvider DirFrame0(this SpriteSpecifier specifier)
        {
            return specifier.RsiStateLike();
        }

        public static IRsiStateLike RsiStateLike(this SpriteSpecifier specifier)
        {
            var resC = IoCManager.Resolve<IResourceCache>();
            switch (specifier)
            {
                case SpriteSpecifier.Texture tex:
                    return tex.GetTexture(resC);

                case SpriteSpecifier.Rsi rsi:
                    return rsi.GetState(resC);

                case SpriteSpecifier.EntityPrototype prototypeIcon:
                    var protMgr = IoCManager.Resolve<IPrototypeManager>();
                    if (!protMgr.TryIndex<EntityPrototype>(prototypeIcon.EntityPrototypeId, out var prototype))
                    {
                        Logger.Error("Failed to load PrototypeIcon {0}", prototypeIcon.EntityPrototypeId);
                        return SpriteComponent.GetFallbackState(resC);
                    }

                    return SpriteComponent.GetPrototypeIcon(prototype, resC);

                default:
                    throw new NotSupportedException();
            }
        }
    }
}

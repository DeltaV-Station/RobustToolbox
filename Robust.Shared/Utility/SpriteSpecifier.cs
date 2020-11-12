using System;
using Robust.Shared.Serialization;
using YamlDotNet.RepresentationModel;

namespace Robust.Shared.Utility
{
    // >tfw you're not using Rust and you don't have easy sum types.
    // pub enum SpriteSpecifier {
    //      Rsi { path: ResourcePath, state: String, },
    //      Texture(ResourcePath),
    // }
    /// <summary>
    ///     Is a reference to EITHER an RSI + RSI State, OR a bare texture path.
    /// </summary>
    [Serializable, NetSerializable]
    public class SpriteSpecifier
    {
        public static readonly SpriteSpecifier Invalid = new Texture(new ResourcePath("."));

        public static SpriteSpecifier FromYaml(YamlNode node)
        {
            if (node is YamlScalarNode)
            {
                return new Texture(node.AsResourcePath());
            }
            if (node is YamlMappingNode mapping)
            {
                return new Rsi(mapping["sprite"].AsResourcePath(), mapping["state"].AsString());
            }
            throw new InvalidOperationException();
        }

        [Serializable, NetSerializable]
        public sealed class Rsi : SpriteSpecifier
        {
            public readonly ResourcePath RsiPath;
            public readonly string RsiState;

            public Rsi(ResourcePath rsiPath, string rsiState)
            {
                RsiPath = rsiPath;
                RsiState = rsiState;
            }

            public override bool Equals(object? obj)
            {
                return (obj is Rsi rsi) && rsi.RsiPath == RsiPath && rsi.RsiState == RsiState;
            }

            public override int GetHashCode()
            {
                return RsiPath.GetHashCode() ^ RsiState.GetHashCode();
            }
        }

        [Serializable, NetSerializable]
        public sealed class Texture : SpriteSpecifier
        {
            public readonly ResourcePath TexturePath;

            public Texture(ResourcePath texturePath)
            {
                TexturePath = texturePath;
            }

            public override bool Equals(object? obj)
            {
                return (obj is Texture texture) && texture.TexturePath == TexturePath;
            }

            public override int GetHashCode()
            {
                return TexturePath.GetHashCode();
            }
        }

        [Serializable, NetSerializable]
        public sealed class PrototypeIcon : SpriteSpecifier
        {
            public readonly string PrototypeId;

            public PrototypeIcon(string prototypeId)
            {
                PrototypeId = prototypeId;
            }

            public override bool Equals(object? obj)
            {
                if (obj is PrototypeIcon prototypeIcon)
                    return PrototypeId == prototypeIcon.PrototypeId;
                return false;
            }

            public override int GetHashCode()
            {
                return PrototypeId.GetHashCode();
            }
        }
    }
}

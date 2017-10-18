using SS14.Shared.Maths;

namespace SS14.Client.Graphics.Sprites
{
    public static class Limits
    {
        public const byte ClickthroughLimit = 64; //default alpha for limiting clickthrough on sprites; will probably be template-dependent later on
    }

    public struct SpriteInfo
    {
        public string Name;
        public Vector2 Offsets;
        public Vector2 Size;
    }
}

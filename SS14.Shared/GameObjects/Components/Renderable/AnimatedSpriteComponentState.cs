﻿using System;

namespace SS14.Shared.GameObjects
{
    [Serializable]
    public class AnimatedSpriteComponentState : RenderableComponentState
    {
        public readonly string CurrentAnimation;
        public readonly bool Loop;
        public readonly string Name;
        public readonly bool Visible;

        public AnimatedSpriteComponentState(bool visible, DrawDepth drawDepth, string name, string currentAnimation,
            bool loop, int? masterUid)
            : base(drawDepth, masterUid, NetIDs.ANIMATED_SPRITE)
        {
            Visible = visible;
            CurrentAnimation = currentAnimation;
            Loop = loop;
            Name = name;
        }
    }
}

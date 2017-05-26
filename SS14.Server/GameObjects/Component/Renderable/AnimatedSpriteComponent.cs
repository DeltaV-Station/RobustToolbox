﻿using SS14.Server.Interfaces.GOC;
using SS14.Shared.GameObjects;
using SS14.Shared.GameObjects.Components.Renderable;
using System;
using System.Collections.Generic;

namespace SS14.Server.GameObjects
{
    public class AnimatedSpriteComponent : Component, IRenderableComponent
    {
        protected IRenderableComponent master;
        protected List<IRenderableComponent> slaves;
        public string Name;
        private string _currentAnimation;
        public string CurrentAnimation
        {
            get
            {
                if(master != null && master.GetType() == typeof(AnimatedSpriteComponent))
                {
                    return ((AnimatedSpriteComponent)master).CurrentAnimation;
                }
                return _currentAnimation;
            }
            set { _currentAnimation = value; }
        }

        private bool _loop = true;
        public bool Loop
        {
            get
            {
                if (master != null && master.GetType() == typeof(AnimatedSpriteComponent))
                {
                    return ((AnimatedSpriteComponent)master).Loop;
                }
                return _loop;
            }
            set { _loop = value; }
        }
        public DrawDepth DrawDepth = DrawDepth.FloorTiles;
        public bool Visible { get; set; }


        public AnimatedSpriteComponent()
        {
            Family = ComponentFamily.Renderable;
            slaves = new List<IRenderableComponent>();
            Visible = true;
        }

        public override ComponentState GetComponentState()
        {
            var masterUid = master != null ? (int?)master.Owner.Uid : null;
            return new AnimatedSpriteComponentState(Visible, DrawDepth, Name, CurrentAnimation, Loop, masterUid);
        }

        public override void SetParameter(ComponentParameter parameter)
        {
            base.SetParameter(parameter);
            switch (parameter.MemberName)
            {
                case "drawdepth":
                    DrawDepth = (DrawDepth)Enum.Parse(typeof(DrawDepth), parameter.GetValue<string>(), true);
                    break;

                case "sprite":
                    Name = parameter.GetValue<string>();
                    break;
            }
        }

        public void SetAnimationState(string state, bool loop = true)
        {
            CurrentAnimation = state;
            Loop = loop;
        }

        public bool IsSlaved()
        {
            return master != null;
        }

        public void SetMaster(Entity m)
        {
            if (!m.HasComponent(ComponentFamily.Renderable))
                return;
            var mastercompo = m.GetComponent<IRenderableComponent>(ComponentFamily.Renderable);
            //If there's no sprite component, then FUCK IT
            if (mastercompo == null)
                return;

            mastercompo.AddSlave(this);
            master = mastercompo;
        }

        public void UnsetMaster()
        {
            if (master == null)
                return;
            master.RemoveSlave(this);
            master = null;
        }

        public void AddSlave(IRenderableComponent slavecompo)
        {
            slaves.Add(slavecompo);
        }

        public void RemoveSlave(IRenderableComponent slavecompo)
        {
            if (slaves.Contains(slavecompo))
                slaves.Remove(slavecompo);
        }
    }
}

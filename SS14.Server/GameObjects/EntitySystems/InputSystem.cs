﻿using SS14.Shared;
using SS14.Shared.GameObjects;
using SS14.Shared.GameObjects.System;

namespace SS14.Server.GameObjects.EntitySystems
{
    public class InputSystem : EntitySystem
    {
        public InputSystem(EntityManager em, EntitySystemManager esm)
            : base(em, esm)
        {
            EntityQuery = new EntityQuery();
            EntityQuery.OneSet.Add(typeof (KeyBindingInputComponent));
        }

        public override void Update(float frametime)
        {
            var entities = EntityManager.GetEntities(EntityQuery);
            foreach(var entity in entities)
            {
                var inputs = entity.GetComponent<KeyBindingInputComponent>(ComponentFamily.Input);

                //Animation setting
                if(entity.GetComponent(ComponentFamily.Renderable) is AnimatedSpriteComponent)
                {
                    var animation = entity.GetComponent<AnimatedSpriteComponent>(ComponentFamily.Renderable);
                    if (inputs.GetKeyState(BoundKeyFunctions.MoveRight) ||
                        inputs.GetKeyState(BoundKeyFunctions.MoveDown) ||
                        inputs.GetKeyState(BoundKeyFunctions.MoveLeft) ||
                        inputs.GetKeyState(BoundKeyFunctions.MoveUp))
                    {
                        if (inputs.GetKeyState(BoundKeyFunctions.Run))
                        {
                            animation.SetAnimationState("run");
                        }
                        else
                        {
                            animation.SetAnimationState("walk");
                        }
                    }
                    else
                    {
                        animation.SetAnimationState("idle");
                    }
                }
            }
        }
    }
}

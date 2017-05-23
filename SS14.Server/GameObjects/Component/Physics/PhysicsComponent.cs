﻿using SS14.Shared.GameObjects;
using SS14.Shared.GameObjects.Components.Physics;
using SS14.Shared.IoC;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace SS14.Server.GameObjects
{
    [IoCTarget]
    [Component("Physics")]
    public class PhysicsComponent : Component
    {
        public float Mass { get; set; }

        public PhysicsComponent()
        {
            Family = ComponentFamily.Physics;
        }

        public override void Update(float frameTime)
        {
            /*if (Owner.GetComponent<SlaveMoverComponent>(ComponentFamily.Mover) != null)
                // If we are being moved by something else right now (like being carried) dont be affected by physics
                return;

            GasEffect();*/
        }

        //private void GasEffect()
        //{

        //    ITile t = IoCManager.Resolve<IMapManager>().GetFloorAt(Owner.GetComponent<TransformComponent>(ComponentFamily.Transform).Position);
        //    if (t == null)
        //        return;
        //    Vector2 gasVel = t.GasCell.GasVelocity;
        //    if (gasVel.Abs() > Mass) // Stop tiny wobbles
        //    {
        //        Owner.SendMessage(this, ComponentMessageType.PhysicsMove,
        //                          Owner.GetComponent<TransformComponent>(ComponentFamily.Transform).Position.X +
        //                          gasVel.X,
        //                          Owner.GetComponent<TransformComponent>(ComponentFamily.Transform).Position.Y +
        //                          gasVel.Y);
        //    }
        //}

        public override void LoadParameters(YamlMappingNode mapping)
        {
            YamlNode node;
            if (mapping.Children.TryGetValue(new YamlScalarNode("mass"), out node))
            {
                Mass = float.Parse(((YamlScalarNode)node).Value);
            }
        }

        public override List<ComponentParameter> GetParameters()
        {
            List<ComponentParameter> cparams = base.GetParameters();
            return cparams;
        }

        public override ComponentState GetComponentState()
        {
            return new PhysicsComponentState(Mass);
        }
    }
}

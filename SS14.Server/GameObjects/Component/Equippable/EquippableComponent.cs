﻿using SS14.Shared;
using SS14.Shared.GameObjects;
using SS14.Shared.GameObjects.Components.Equippable;
using SS14.Shared.IoC;
using System;
using YamlDotNet.RepresentationModel;

namespace SS14.Server.GameObjects
{
    [IoCTarget]
    [Component("Equippable")]
    public class EquippableComponent : Component
    {
        public EquipmentSlot wearloc;

        public Entity currentWearer { get; set; }

        public EquippableComponent()
        {
            Family = ComponentFamily.Equippable;
        }

        public override ComponentReplyMessage RecieveMessage(object sender, ComponentMessageType type,
                                                             params object[] list)
        {
            ComponentReplyMessage reply = base.RecieveMessage(sender, type, list);

            if (sender == this)
                return ComponentReplyMessage.Empty;

            switch (type)
            {
                case ComponentMessageType.GetWearLoc:
                    reply = new ComponentReplyMessage(ComponentMessageType.ReturnWearLoc, wearloc);
                    break;
            }

            return reply;
        }

        public override void LoadParameters(YamlMappingNode mapping)
        {
            base.LoadParameters(mapping);

            var node = (YamlScalarNode)mapping["wearloc"];

            wearloc = (EquipmentSlot) Enum.Parse(typeof (EquipmentSlot), node.Value);
        }

        public override ComponentState GetComponentState()
        {
            return new EquippableComponentState(wearloc, currentWearer != null ? currentWearer.Uid : (int?)null);
        }
    }
}

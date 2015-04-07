﻿using Lidgren.Network;
using SS14.Client.Interfaces.UserInterface;
using SS14.Shared;
using SS14.Shared.GO;
using SS14.Shared.GO.Component.Damageable.Health.LocationalHealth;

using System;
using System.Collections.Generic;
using System.Linq;

namespace SS14.Client.GameObjects
{
    /// <summary>
    /// Behaves like health component but tracks damage of individual zones.
    /// This is for mobs.
    /// </summary>
    public class HumanHealthComponent : HealthComponent
    {
        public List<DamageLocation> DamageZones = new List<DamageLocation>();

        public override Type StateType
        {
            get { return typeof (HumanHealthComponentState); }
        }

        public override void HandleNetworkMessage(IncomingEntityComponentMessage message, NetConnection sender)
        {
            var type = (ComponentMessageType) message.MessageParameters[0];

            switch (type)
            {
                // TODO refactor me -- health status data should flow entirely via states -- maybe this is true, but right now its using both...
                case (ComponentMessageType.HealthStatus):
                    HandleHealthUpdate(message);
                    break;
            }
        }

        public override ComponentReplyMessage RecieveMessage(object sender, ComponentMessageType type,
                                                             params object[] list)
        {
            ComponentReplyMessage reply = base.RecieveMessage(sender, type, list);

            if (sender == this) //Don't listen to our own messages!
                return ComponentReplyMessage.Empty;

            switch (type)
            {
                // TODO refactor me - GUI should reference component directly rather than doing this message shit
                case ComponentMessageType.GetCurrentLocationHealth:
                    var location = (BodyPart) list[0];
                    if (DamageZones.Exists(x => x.Location == location))
                    {
                        DamageLocation dmgLoc = DamageZones.First(x => x.Location == location);
                        reply = new ComponentReplyMessage(ComponentMessageType.CurrentLocationHealth, location,
                                                          dmgLoc.UpdateTotalHealth(), dmgLoc.MaxHealth);
                    }
                    break;
            }

            return reply;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="msg"></param>
        [Obsolete("This is the old way of doing things -- this should be removed and this component should be checked to make sure the state system handles its work.")]
        public void HandleHealthUpdate(IncomingEntityComponentMessage msg)
        {
            var part = (BodyPart) msg.MessageParameters[1];
            var dmgCount = (int) msg.MessageParameters[2];
            var maxHP = (int) msg.MessageParameters[3];

            if (DamageZones.Exists(x => x.Location == part))
            {
                DamageLocation existingZone = DamageZones.First(x => x.Location == part);
                existingZone.MaxHealth = maxHP;

                for (int i = 0; i < dmgCount; i++)
                {
                    var type = (DamageType) msg.MessageParameters[4 + (i*2)];
                    //Retrieve data from message in pairs starting at 4
                    var amount = (int) msg.MessageParameters[5 + (i*2)];

                    if (existingZone.DamageIndex.ContainsKey(type))
                        existingZone.DamageIndex[type] = amount;
                    else
                        existingZone.DamageIndex.Add(type, amount);
                }

                existingZone.UpdateTotalHealth();
            }
            else
            {
                var newZone = new DamageLocation(part, maxHP, maxHP);
                DamageZones.Add(newZone);

                for (int i = 0; i < dmgCount; i++)
                {
                    var type = (DamageType) msg.MessageParameters[4 + (i*2)];
                    //Retrieve data from message in pairs starting at 4
                    var amount = (int) msg.MessageParameters[5 + (i*2)];

                    if (newZone.DamageIndex.ContainsKey(type))
                        newZone.DamageIndex[type] = amount;
                    else
                        newZone.DamageIndex.Add(type, amount);
                }

                newZone.UpdateTotalHealth();
            }

            MaxHealth = GetMaxHealth();
            Health = GetHealth();
            if (Health <= 0) Die(); //Need better logic here.

            IoCManager.Resolve<IUserInterfaceManager>().ComponentUpdate(GuiComponentType.TargetingUi);
        }

        public override float GetMaxHealth()
        {
            return DamageZones.Sum(x => x.MaxHealth);
        }

        public override float GetHealth()
        {
            return DamageZones.Sum(x => x.UpdateTotalHealth());
        }

        public override void HandleComponentState(dynamic state)
        {
            base.HandleComponentState((HumanHealthComponentState) state);

            foreach (LocationHealthState locstate in state.LocationHealthStates)
            {
                BodyPart part = locstate.Location;
                int maxHP = locstate.MaxHealth;

                if (DamageZones.Exists(x => x.Location == part))
                {
                    DamageLocation existingZone = DamageZones.First(x => x.Location == part);
                    existingZone.MaxHealth = maxHP;

                    foreach (var kvp in locstate.DamageIndex)
                    {
                        DamageType type = kvp.Key;
                        int amount = kvp.Value;

                        if (existingZone.DamageIndex.ContainsKey(type))
                            existingZone.DamageIndex[type] = amount;
                        else
                            existingZone.DamageIndex.Add(type, amount);
                    }

                    existingZone.UpdateTotalHealth();
                }
                else
                {
                    var newZone = new DamageLocation(part, maxHP, maxHP);
                    DamageZones.Add(newZone);

                    foreach (var kvp in locstate.DamageIndex)
                    {
                        DamageType type = kvp.Key;
                        int amount = kvp.Value;

                        if (newZone.DamageIndex.ContainsKey(type))
                            newZone.DamageIndex[type] = amount;
                        else
                            newZone.DamageIndex.Add(type, amount);
                    }

                    newZone.UpdateTotalHealth();
                }

                MaxHealth = GetMaxHealth();
                Health = GetHealth();
                if (Health <= 0) Die(); //Need better logic here.

                IoCManager.Resolve<IUserInterfaceManager>().ComponentUpdate(GuiComponentType.TargetingUi);
            }
        }
    }
}
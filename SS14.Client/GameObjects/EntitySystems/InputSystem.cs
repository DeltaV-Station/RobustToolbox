﻿using SS14.Client.GameObjects.Components;
using SS14.Client.Interfaces.Input;
using SS14.Shared.GameObjects;
using SS14.Shared.GameObjects.Systems;
using SS14.Shared.Input;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.IoC;
using SS14.Shared.Log;
using SS14.Shared.Players;

namespace SS14.Client.GameObjects.EntitySystems
{
    class InputSystem : EntitySystem
    {
        private readonly IPlayerCommandStates _cmdStates = new PlayerCommandStates();
        private readonly CommandBindMapping _bindMap = new CommandBindMapping();

        public IPlayerCommandStates CmdStates => _cmdStates;
        public ICommandBindMapping BindMap => _bindMap;

        public void HandleInputCommand(ICommonSession session, BoundKeyFunction function, FullInputCmdMessage message)
        {
            Logger.DebugS("input.command", $"{function}: state={message.State}, uid={message.Uid}");

            // set state, state change is updated regardless if it is locally bound
            _cmdStates.SetState(function, message.State);

            // handle local binds before sending off
            if (_bindMap.TryGetHandler(function, out var handler))
            {
                // local handlers can block sending over the network.
                if (handler.HandleCmdMessage(session, message))
                    return;
            }

            RaiseNetworkEvent(message);
        }

        public override void SubscribeEvents()
        {
            base.SubscribeEvents();

            SubscribeEvent<PlayerAttachSysMessage>(OnAttachedEntityChanged);
        }

        private void OnAttachedEntityChanged(object sender, EntitySystemMessage message)
        {
            if(!(message is PlayerAttachSysMessage msg))
                return;

            if (msg.AttachedEntity != null) // attach
            {
                if(!msg.AttachedEntity.TryGetComponent(out InputComponent inputComp))
                {
                    Logger.DebugS("input.context", $"AttachedEnt has no InputComponent: entId={msg.AttachedEntity.Uid}, entProto={msg.AttachedEntity.Prototype}");
                    return;
                }

                var inputMan = IoCManager.Resolve<IInputManager>();

                if(inputMan.Contexts.Exists(inputComp.ContextName))
                {
                    inputMan.Contexts.SetActiveContext(inputComp.ContextName);
                }
                else
                {
                    Logger.ErrorS("input.context", $"Unknown context: entId={msg.AttachedEntity.Uid}, entProto={msg.AttachedEntity.Prototype}, context={inputComp.ContextName}");
                }
            }
            else // detach
            {
                var inputMan = IoCManager.Resolve<IInputManager>();
                inputMan.Contexts.SetActiveContext(InputContextContainer.DefaultContextName);
            }
        }
    }

    public class PlayerAttachSysMessage : EntitySystemMessage
    {
        public IEntity AttachedEntity { get; }

        public PlayerAttachSysMessage(IEntity attachedEntity)
        {
            AttachedEntity = attachedEntity;
        }
    }
}

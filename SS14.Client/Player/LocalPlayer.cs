﻿using System;
using SS14.Client.GameObjects;
using SS14.Client.Interfaces.GameObjects;
using SS14.Shared;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.Interfaces.GameObjects.Components;
using SS14.Shared.IoC;
using SS14.Shared.Players;

namespace SS14.Client.Player
{
    public class LocalPlayer
    {
        public EventHandler EntityAttached;
        public EventHandler EntityDetatched;

        /// <summary>
        ///     Game entity that the local player is controlling. If this is null, the player
        ///     is in free/spectator cam.
        /// </summary>
        public IEntity ControlledEntity { get; private set; }

        public PlayerIndex Index { get; set; }

        public PlayerSession Session { get; set; }

        public event EventHandler<MoveEventArgs> EntityMoved;
        public event EventHandler<StatusEventArgs> StatusChanged;

        /// <summary>
        ///     Attaches a client to an entity.
        /// </summary>
        /// <param name="entity">Entity to attach the client to.</param>
        public void AttachEntity(IEntity entity)
        {
            // Detach and cleanup first
            DetatchEntity();

            var factory = IoCManager.Resolve<IComponentFactory>();

            ControlledEntity = entity;
            ControlledEntity.AddComponent(factory.GetComponent<KeyBindingInputComponent>());

            if (ControlledEntity.HasComponent<IMoverComponent>())
                ControlledEntity.RemoveComponent<IMoverComponent>();

            ControlledEntity.AddComponent(factory.GetComponent<PlayerInputMoverComponent>());

            if (!ControlledEntity.HasComponent<CollidableComponent>())
                ControlledEntity.AddComponent(factory.GetComponent<CollidableComponent>());

            ControlledEntity.GetComponent<ITransformComponent>().OnMove += OnPlayerMoved;

            EntityAttached?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        ///     Detaches the client from an entity.
        /// </summary>
        public void DetatchEntity()
        {
            if (ControlledEntity != null && ControlledEntity.Initialized)
            {
                ControlledEntity.RemoveComponent<KeyBindingInputComponent>();
                ControlledEntity.RemoveComponent<PlayerInputMoverComponent>();
                ControlledEntity.RemoveComponent<CollidableComponent>();
                var transform = ControlledEntity.GetComponent<ITransformComponent>();
                if (transform != null)
                    transform.OnMove -= OnPlayerMoved;
            }
            ControlledEntity = null;

            EntityDetatched?.Invoke(this, EventArgs.Empty);
        }

        private void OnPlayerMoved(object sender, MoveEventArgs args)
        {
            EntityMoved?.Invoke(sender, args);
        }

        public void SwitchState(SessionStatus newStatus)
        {
            var args = new StatusEventArgs(Session.Status, newStatus);
            Session.Status = newStatus;
            StatusChanged?.Invoke(this, args);
        }
    }

    public class StatusEventArgs : EventArgs
    {
        public SessionStatus OldStatus { get; }
        public SessionStatus NewStatus { get; }

        public StatusEventArgs(SessionStatus oldStatus, SessionStatus newStatus)
        {
            OldStatus = oldStatus;
            NewStatus = newStatus;
        }
    }
}

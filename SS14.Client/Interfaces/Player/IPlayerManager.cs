﻿using Lidgren.Network;
using SS14.Client.Graphics.Render;
using SS14.Shared;
using SS14.Shared.GameObjects;
using SS14.Shared.GameStates;
using SS14.Shared.IoC;
using System;
using System.Collections.Generic;
using KeyboardKeys = SFML.Window.Keyboard.Key;

namespace SS14.Client.Interfaces.Player
{
    public interface IPlayerManager : IIoCInterface
    {
        Entity ControlledEntity { get; }

        event EventHandler<TypeEventArgs> RequestedStateSwitch;
        event EventHandler<VectorEventArgs> OnPlayerMove;

        void Attach(Entity newEntity);
        void Detach();
        void SendVerb(string verb, int uid);
        void KeyDown(KeyboardKeys key);
        void KeyUp(KeyboardKeys key);
        void HandleNetworkMessage(NetIncomingMessage message);
        void Update(float frameTime);
        void ApplyEffects(RenderImage image);

        void ApplyPlayerStates(List<PlayerState> list);
    }
}

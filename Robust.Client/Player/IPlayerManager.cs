﻿using System;
using System.Collections.Generic;
using Robust.Shared.GameStates;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.Network;
using Robust.Shared.Players;

namespace Robust.Client.Player
{
    public interface IPlayerManager
    {
        IEnumerable<IPlayerSession> Sessions { get; }
        IReadOnlyDictionary<NetUserId, IPlayerSession> SessionsDict { get; }

        LocalPlayer? LocalPlayer { get; }

        /// <summary>
        /// Invoked after LocalPlayer is changed
        /// </summary>
        event Action<LocalPlayerChangedEventArgs>? LocalPlayerChanged;

        int PlayerCount { get; }
        int MaxPlayers { get; }
        event EventHandler PlayerListUpdated;

        void Initialize();
        void Startup(INetChannel channel);
        void Update(float frameTime);
        void Shutdown();

        void ApplyPlayerStates(IEnumerable<PlayerState>? list);
    }

    public class LocalPlayerChangedEventArgs : EventArgs
    {
        public readonly LocalPlayer? OldPlayer;
        public readonly LocalPlayer? NewPlayer;
        public LocalPlayerChangedEventArgs(LocalPlayer? oldPlayer, LocalPlayer? newPlayer)
        {
            OldPlayer = oldPlayer;
            NewPlayer = newPlayer;
        }
    }
}

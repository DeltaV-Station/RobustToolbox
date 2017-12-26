﻿using System;
using System.Collections.Generic;
using SS14.Shared.Console;
using SS14.Shared.Interfaces.Network;
using SS14.Shared.Players;

namespace SS14.Server.Interfaces.Chat
{
    public interface IChatManager
    {
        /// <summary>
        ///     Sets up the ChatManager into a usable state.
        /// </summary>
        void Initialize();

        /// <summary>
        ///     Sends a chat message to a single client.
        /// </summary>
        /// <param name="client">Clients to send the message to.</param>
        /// <param name="channel">Channel that the chat is broadcast on.</param>
        /// <param name="text">Text to broadcast.</param>
        /// <param name="index">Optional PlayerIndex of the client that the message is bound to.</param>
        /// <param name="entityUid">Optional entity Uid that the message is bound to.</param>
        void DispatchMessage(INetChannel client, ChatChannel channel, string text, PlayerIndex? index = null, int? entityUid = null);

        /// <summary>
        ///     Sends a chat message to multiple clients.
        /// </summary>
        /// <param name="clients"></param>
        /// <param name="channel">Channel that the chat is broadcast on.</param>
        /// <param name="text">Text to broadcast.</param>
        /// <param name="index">Optional PlayerIndex of the client that the message is bound to.</param>
        /// <param name="entityUid">Optional entity Uid that the message is bound to.</param>
        void DispatchMessage(List<INetChannel> clients, ChatChannel channel, string text, PlayerIndex? index = null, int? entityUid = null);

        /// <summary>
        ///     Sends a chat message to all connected clients.
        /// </summary>
        /// <param name="channel">Channel that the chat is broadcast on.</param>
        /// <param name="text">Text to broadcast.</param>
        /// <param name="index">Optional PlayerIndex of the client that the message is bound to.</param>
        /// <param name="entityUid">Optional entity Uid that the message is bound to.</param>
        void DispatchMessage(ChatChannel channel, string text, PlayerIndex? index = null, int? entityUid = null);

        [Obsolete("Use DispatchMessage")]
        void SendChatMessage(ChatChannel channel, string text, string name, int? entityID);
    }
}

﻿using System;
using Lidgren.Network;
using SS14.Shared.Network;

namespace SS14.Shared.Interfaces.Network
{
    /// <summary>
    ///     A network channel between this peer and a remote peer.
    /// </summary>
    public interface INetChannel
    {
        /// <summary>
        ///     The raw connection of this channel.
        /// </summary>
        [Obsolete("Use a NetChannel/PlayerSession instead.")]
        NetConnection Connection { get; }

        /// <summary>
        ///     The NetPeer this belongs to.
        /// </summary>
        INetManager NetPeer { get; }

        /// <summary>
        ///     The player slot ID of this channel.
        /// </summary>
        int NetworkId { get; set; }

        /// <summary>
        ///     The Unique Identifier of the connection.
        /// </summary>
        long ConnectionId { get; }

        /// <summary>
        ///     The IP address of the remote peer.
        /// </summary>
        string RemoteAddress { get; }

        /// <summary>
        ///     Average round trip time in milliseconds between the remote peer and us.
        /// </summary>
        int Ping { get; }

        /// <summary>
        ///     Creates a new NetMessage to be filled up and sent.
        /// </summary>
        /// <typeparam name="T">The derived NetMessage type to send.</typeparam>
        /// <returns>A new instance of the net message.</returns>
        T CreateNetMessage<T>()
            where T : NetMessage;

        /// <summary>
        ///     Sends a NetMessage over this NetChannel.
        /// </summary>
        /// <param name="message">The NetMessage to send.</param>
        void SendMessage(NetMessage message);
    }
}

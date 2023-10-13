﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Robust.Shared.Network
{
    /// <summary>
    /// Arguments for NetChannel events.
    /// </summary>
    [Virtual]
    public class NetChannelArgs : EventArgs
    {
        /// <summary>
        ///     The channel causing the event.
        /// </summary>
        public readonly INetChannel Channel;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="channel">The channel causing the event.</param>
        public NetChannelArgs(INetChannel channel)
        {
            Channel = channel;
        }
    }

    /// <summary>
    /// Arguments for incoming connection event.
    /// </summary>
    public sealed class NetConnectingArgs : EventArgs
    {
        public bool IsDenied => DenyReasonData != null;

        public string? DenyReason => DenyReasonData?.Text;
        public NetDenyReason? DenyReasonData { get; private set; }

        public NetUserData UserData { get; }

        /// <summary>
        /// The IP of the incoming connection.
        /// </summary>
        public NetUserId UserId => UserData.UserId;
        public string UserName => UserData.UserName;

        public IPEndPoint IP { get; }
        public LoginType AuthType { get; }

        public void Deny(string reason)
        {
            Deny(new NetDenyReason(reason));
        }

        public void Deny(NetDenyReason reason)
        {
            DenyReasonData = reason;
        }

        /// <summary>
        ///     Constructs a new instance.
        /// </summary>
        /// <param name="data">The user data of the incoming connection.</param>
        /// <param name="ip"></param>
        /// <param name="authType">The type of authentication to use when connecting.</param>
        public NetConnectingArgs(NetUserData data, IPEndPoint ip, LoginType authType)
        {
            UserData = data;
            IP = ip;
            AuthType = authType;
        }
    }

    /// <summary>
    /// Contains a reason for denying a client connection to the game server.
    /// </summary>
    /// <param name="Text">The textual reason, presented to the user.</param>
    /// <param name="AdditionalProperties">Additional JSON properties that will be included in the <see cref="NetStructuredDisconnectMessages"/>.</param>
    /// <seealso cref="NetStructuredDisconnectMessages"/>
    /// <seealso cref="NetConnectingArgs"/>
    public record NetDenyReason(string Text, Dictionary<string, JsonNode> AdditionalProperties)
    {
        public NetDenyReason(string Text) : this(Text, new Dictionary<string, JsonNode>())
        {
        }
    }

    /// <summary>
    /// Structured reason common interface.
    /// </summary>
    public interface INetStructuredReason
    {
        JsonObject StructuredReason { get; }
        string Reason { get; }
        bool RedialFlag { get; }
    }

    /// <summary>
    /// Arguments for a failed connection attempt.
    /// </summary>
    public sealed class NetConnectFailArgs : EventArgs, INetStructuredReason
    {
        public NetConnectFailArgs(string reason) : this(NetStructuredDisconnectMessages.Decode(reason))
        {
        }

        public NetConnectFailArgs(JsonObject reason)
        {
            StructuredReason = reason;
        }

        public JsonObject StructuredReason { get; }
        public string Reason => NetStructuredDisconnectMessages.ReasonOf(StructuredReason);
        public bool RedialFlag => NetStructuredDisconnectMessages.RedialFlagOf(StructuredReason);
    }

    public sealed class NetDisconnectedArgs : NetChannelArgs, INetStructuredReason
    {
        public NetDisconnectedArgs(INetChannel channel, string reason) : this(channel, NetStructuredDisconnectMessages.Decode(reason))
        {
        }

        public NetDisconnectedArgs(INetChannel channel, JsonObject reason) : base(channel)
        {
            StructuredReason = reason;
        }

        public JsonObject StructuredReason { get; }
        public string Reason => NetStructuredDisconnectMessages.ReasonOf(StructuredReason);
        public bool RedialFlag => NetStructuredDisconnectMessages.RedialFlagOf(StructuredReason);
    }
}

﻿using Lidgren.Network;
using SS14.Shared.Interfaces.Network;

namespace SS14.Shared.Network.Messages
{
    public class MsgServerInfo : NetMessage
    {
        #region REQUIRED

        public static readonly string NAME = "WelcomeMessage";
        public static readonly MsgGroups GROUP = MsgGroups.Core;
        public static readonly NetMessages ID = NetMessages.WelcomeMessage;

        public MsgServerInfo(INetChannel channel)
            : base(NAME, GROUP, ID)
        {
        }

        #endregion

        public string ServerName { get; set; }
        public int ServerPort { get; set; }
        public string ServerWelcomeMessage { get; set; }
        public int ServerMaxPlayers { get; set; }
        public string ServerMapName { get; set; }
        public string GameMode { get; set; }
        public int ServerPlayerCount { get; set; }

        public override void ReadFromBuffer(NetIncomingMessage buffer)
        {
            ServerName = buffer.ReadString();
            ServerPort = buffer.ReadInt32();
            ServerWelcomeMessage = buffer.ReadString();
            ServerMaxPlayers = buffer.ReadInt32();
            ServerMapName = buffer.ReadString();
            GameMode = buffer.ReadString();
            ServerPlayerCount = buffer.ReadInt32();
        }

        public override void WriteToBuffer(NetOutgoingMessage buffer)
        {
            buffer.Write(ServerName);
            buffer.Write(ServerPort);
            buffer.Write(ServerWelcomeMessage);
            buffer.Write(ServerMaxPlayers);
            buffer.Write(ServerMapName);
            buffer.Write(GameMode);
            buffer.Write(ServerPlayerCount);
        }
    }
}

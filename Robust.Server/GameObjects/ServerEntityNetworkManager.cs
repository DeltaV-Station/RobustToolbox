﻿using System;
using System.Collections.Generic;
using Robust.Server.Interfaces.GameObjects;
using Robust.Server.Interfaces.Player;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.Interfaces.Timing;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network.Messages;
using Robust.Shared.Utility;

namespace Robust.Server.GameObjects
{
    /// <summary>
    /// The server implementation of the Entity Network Manager.
    /// </summary>
    public class ServerEntityNetworkManager : IServerEntityNetworkManager
    {
#pragma warning disable 649
        [Dependency] private readonly IServerNetManager _networkManager;
        [Dependency] private readonly IGameTiming _gameTiming;
        [Dependency] private readonly IPlayerManager _playerManager;
#pragma warning restore 649

        /// <inheritdoc />
        public event EventHandler<NetworkComponentMessage> ReceivedComponentMessage;

        /// <inheritdoc />
        public event EventHandler<object> ReceivedSystemMessage;

        private readonly PriorityQueue<MsgEntity> _queue = new PriorityQueue<MsgEntity>(new MessageSequenceComparer());

        private readonly Dictionary<IPlayerSession, uint> _lastProcessedSequencesCmd =
            new Dictionary<IPlayerSession, uint>();

        /// <inheritdoc />
        public void SetupNetworking()
        {
            _networkManager.RegisterNetMessage<MsgEntity>(MsgEntity.NAME, HandleEntityNetworkMessage);

            _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        }

        public void Update()
        {
            while (_queue.Count != 0 && _queue.Peek().SourceTick <= _gameTiming.CurTick)
            {
                DispatchEntityNetworkMessage(_queue.Take());
            }
        }

        public uint GetLastMessageSequence(IPlayerSession session)
        {
            return _lastProcessedSequencesCmd[session];
        }

        /// <inheritdoc />
        public void SendComponentNetworkMessage(INetChannel channel, IEntity entity, IComponent component,
            ComponentMessage message)
        {
            if (_networkManager.IsClient)
                return;

            if (!component.NetID.HasValue)
                throw new ArgumentException($"Component {component.Name} does not have a NetID.", nameof(component));

            var msg = _networkManager.CreateNetMessage<MsgEntity>();
            msg.Type = EntityMessageType.ComponentMessage;
            msg.EntityUid = entity.Uid;
            msg.NetId = component.NetID.Value;
            msg.ComponentMessage = message;
            msg.SourceTick = _gameTiming.CurTick;

            //Send the message
            if (channel == null)
                _networkManager.ServerSendToAll(msg);
            else
                _networkManager.ServerSendMessage(msg, channel);
        }

        /// <inheritdoc />
        public void SendSystemNetworkMessage(EntitySystemMessage message)
        {
            var newMsg = _networkManager.CreateNetMessage<MsgEntity>();
            newMsg.Type = EntityMessageType.SystemMessage;
            newMsg.SystemMessage = message;
            newMsg.SourceTick = _gameTiming.CurTick;

            _networkManager.ServerSendToAll(newMsg);
        }

        /// <inheritdoc />
        public void SendSystemNetworkMessage(EntitySystemMessage message, INetChannel targetConnection)
        {
            var newMsg = _networkManager.CreateNetMessage<MsgEntity>();
            newMsg.Type = EntityMessageType.SystemMessage;
            newMsg.SystemMessage = message;
            newMsg.SourceTick = _gameTiming.CurTick;

            _networkManager.ServerSendMessage(newMsg, targetConnection);
        }

        private void HandleEntityNetworkMessage(MsgEntity message)
        {
            var msgT = message.SourceTick;
            var cT = _gameTiming.CurTick;
            Logger.Info($"{msgT}, {cT}");
            if (msgT <= cT)
            {
                if (msgT < cT)
                {
                    Logger.WarningS("net.ent", "Got late MsgEntity! Diff: {0}, msgT: {2}, cT: {3}, player: {1}",
                        (int) msgT.Value - (int) cT.Value, message.MsgChannel.SessionId, msgT, cT);
                }

                DispatchEntityNetworkMessage(message);
                return;
            }

            _queue.Add(message);
        }

        private void DispatchEntityNetworkMessage(MsgEntity message)
        {
            var player = _playerManager.GetSessionByChannel(message.MsgChannel);

            if (message.Sequence != 0)
            {
                if (_lastProcessedSequencesCmd[player] < message.Sequence)
                {
                    _lastProcessedSequencesCmd[player] = message.Sequence;
                }
            }

            switch (message.Type)
            {
                case EntityMessageType.ComponentMessage:
                    // Get player session
                    var session = _playerManager.GetSessionByChannel(message.MsgChannel);
                    ReceivedComponentMessage?.Invoke(this, new NetworkComponentMessage(message, session));
                    return;

                case EntityMessageType.SystemMessage:
                    var msg = message.SystemMessage;
                    var sessionType = typeof(EntitySessionMessage<>).MakeGenericType(msg.GetType());
                    var sessionMsg = Activator.CreateInstance(sessionType, new EntitySessionEventArgs(player), msg);
                    ReceivedSystemMessage?.Invoke(this, sessionMsg);
                    return;
            }
        }

        private void OnPlayerStatusChanged(object sender, SessionStatusEventArgs args)
        {
            switch (args.NewStatus)
            {
                case SessionStatus.Connected:
                    _lastProcessedSequencesCmd.Add(args.Session, 0);
                    break;

                case SessionStatus.Disconnected:
                    _lastProcessedSequencesCmd.Remove(args.Session);
                    break;
            }
        }

        private sealed class MessageSequenceComparer : IComparer<MsgEntity>
        {
            public int Compare(MsgEntity x, MsgEntity y)
            {
                DebugTools.AssertNotNull(x);
                DebugTools.AssertNotNull(y);

                return y.Sequence.CompareTo(x.Sequence);
            }
        }
    }
}

﻿using SS14.Shared;
using SS14.Shared.GO;
using SS14.Shared.IoC;

namespace SS14.Server.Interfaces.MessageLogging
{
    public interface IMessageLogger : IIoCInterface
    {
        void LogOutgoingComponentNetMessage(long clientUID, int uid, ComponentFamily family, object[] parameters);

        void LogIncomingComponentNetMessage(long clientUID, int uid, EntityMessage entityMessage,
                                            ComponentFamily componentFamily, object[] parameters);

        void LogComponentMessage(int uid, ComponentFamily senderfamily, string sendertype, ComponentMessageType type);
        void Ping();
    }
}

using System;
using JetBrains.Annotations;
using Lidgren.Network;
using Robust.Shared.Interfaces.Network;
using Robust.Shared.Network;

namespace Robust.Shared.Serialization
{

    /// <summary>
    /// The server part of the string-exchange handshake. Sent as the
    /// first message in the handshake. Tells the client the hash of
    /// the current string mapping, so the client can check if it has
    /// a local copy.
    /// </summary>
    /// <seealso cref="RobustMappedStringSerializer.NetworkInitialize"/>
    [UsedImplicitly]
    internal class MsgRobustMappedStringsSerializerServerHandshake : NetMessage
    {

        public MsgRobustMappedStringsSerializerServerHandshake(INetChannel ch)
            : base(nameof(MsgRobustMappedStringsSerializerServerHandshake), MsgGroups.Core)
        {
        }

        /// <value>
        /// The hash of the current string mapping held by the server.
        /// </value>
        public byte[]? Hash { get; set; }

        /// <value>
        /// The hash of the types held by the server.
        /// </value>
        public byte[]? TypesHash { get; set; }

        public override void ReadFromBuffer(NetIncomingMessage buffer)
        {
            var len = buffer.ReadVariableInt32();
            if (len > 64)
            {
                throw new InvalidOperationException("Hash too long.");
            }

            buffer.ReadBytes(Hash = new byte[len]);

            len = buffer.ReadVariableInt32();
            if (len > 64)
            {
                throw new InvalidOperationException("TypesHash too long.");
            }

            buffer.ReadBytes(TypesHash = new byte[len]);
        }

        public override void WriteToBuffer(NetOutgoingMessage buffer)
        {
            if (Hash == null)
            {
                throw new InvalidOperationException("Hash has not been specified.");
            }

            if (TypesHash == null)
            {
                throw new InvalidOperationException("TypesHash has not been specified.");
            }

            buffer.WriteVariableInt32(Hash.Length);
            buffer.Write(Hash);
            buffer.WriteVariableInt32(TypesHash.Length);
            buffer.Write(TypesHash);
        }

    }

}

using Lidgren.Network;
using Robust.Shared.Serialization;

#nullable disable

namespace Robust.Shared.Network.Messages
{
    public sealed class MsgScriptCompletion : NetMessage
    {
        public override MsgGroups MsgGroup => MsgGroups.Command;

        public int ScriptSession { get; set; }
        public int Cursor { get; set; }
        public string Code { get; set; }

        public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
        {
            ScriptSession = buffer.ReadInt32();
            Cursor = buffer.ReadInt32();
            Code = buffer.ReadString();
        }

        public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
        {
            buffer.Write(ScriptSession);
            buffer.Write(Cursor);
            buffer.Write(Code);
        }
    }
}

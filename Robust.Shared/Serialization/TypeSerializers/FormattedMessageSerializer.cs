﻿using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Utility;

namespace Robust.Shared.Serialization.TypeSerializers
{
    public class FormattedMessageSerializer : ITypeSerializer<FormattedMessage, ValueDataNode>
    {
        public FormattedMessage Read(ValueDataNode node, ISerializationContext? context = null)
        {
            return FormattedMessage.FromMarkup(node.Value);
        }

        public DataNode Write(FormattedMessage value, bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return new ValueDataNode(value.ToString());
        }
    }
}

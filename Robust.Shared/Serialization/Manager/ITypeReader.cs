﻿using Robust.Shared.Serialization.Manager.Result;
using Robust.Shared.Serialization.Markdown;

namespace Robust.Shared.Serialization.Manager
{
    public interface ITypeReader<TType, TNode> where TType : notnull where TNode : DataNode
    {
        DeserializationResult Read(ISerializationManager serializationManager, TNode node,
            ISerializationContext? context = null);
    }
}

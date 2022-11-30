using Linguini.Syntax.Ast;
using Robust.Shared.IoC;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;
using System;
using System.Collections.Generic;

namespace Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;

[TypeSerializer]
public sealed class QueueSerializer<T> : ITypeSerializer<Queue<T>, SequenceDataNode>
{
    Queue<T> ITypeReader<Queue<T>, SequenceDataNode>.Read(ISerializationManager serializationManager,
        SequenceDataNode node,
        IDependencyCollection dependencies,
        bool skipHook,
        ISerializationContext? context, Queue<T>? queue)
    {
        queue ??= new Queue<T>();

        foreach (var dataNode in node.Sequence)
        {
            queue.Enqueue(serializationManager.Read<T>(dataNode, context, skipHook));
        }

        return queue;
    }

    ValidationNode ITypeValidator<Queue<T>, SequenceDataNode>.Validate(ISerializationManager serializationManager,
        SequenceDataNode node, IDependencyCollection dependencies, ISerializationContext? context)
    {
        var list = new List<ValidationNode>();
        foreach (var elem in node.Sequence)
        {
            list.Add(serializationManager.ValidateNode(typeof(T), elem, context));
        }

        return new ValidatedSequenceNode(list);
    }

    public DataNode Write(ISerializationManager serializationManager, Queue<T> value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
    {
        var sequence = new SequenceDataNode();

        foreach (var elem in value)
        {
            sequence.Add(serializationManager.WriteValue(typeof(T), elem, alwaysWrite, context));
        }

        return sequence;
    }

    public Queue<T> Copy(ISerializationManager serializationManager, Queue<T> source, Queue<T> target,
        bool skipHook,
        ISerializationContext? context = null)
    {
        target.Clear();
        target.EnsureCapacity(source.Count);

        foreach (var element in source)
        {
            var elementCopy = serializationManager.Copy(element, context) ?? throw new NullReferenceException();
            target.Enqueue(elementCopy);
        }

        return target;
    }
}

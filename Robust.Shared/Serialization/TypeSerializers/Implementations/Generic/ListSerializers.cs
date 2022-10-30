using System.Collections.Generic;
using System.Collections.Immutable;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Robust.Shared.Serialization.TypeSerializers.Implementations.Generic
{
    [TypeSerializer]
    public sealed class ListSerializers<T> :
        ITypeSerializer<List<T>, SequenceDataNode>,
        ITypeSerializer<IReadOnlyList<T>, SequenceDataNode>,
        ITypeSerializer<IReadOnlyCollection<T>, SequenceDataNode>,
        ITypeSerializer<ImmutableList<T>, SequenceDataNode>,
        ITypeCopier<List<T>>
    {
        private DataNode WriteInternal(ISerializationManager serializationManager, IEnumerable<T> value, bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            var sequence = new SequenceDataNode();

            foreach (var elem in value)
            {
                sequence.Add(serializationManager.WriteValue(typeof(T), elem, alwaysWrite, context));
            }

            return sequence;
        }

        public DataNode Write(ISerializationManager serializationManager, ImmutableList<T> value,
            IDependencyCollection dependencies,
            bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return WriteInternal(serializationManager, value, alwaysWrite, context);
        }

        public DataNode Write(ISerializationManager serializationManager, List<T> value,
            IDependencyCollection dependencies, bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return WriteInternal(serializationManager, value, alwaysWrite, context);
        }

        public DataNode Write(ISerializationManager serializationManager, IReadOnlyCollection<T> value,
            IDependencyCollection dependencies,
            bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return WriteInternal(serializationManager, value, alwaysWrite, context);
        }

        public DataNode Write(ISerializationManager serializationManager, IReadOnlyList<T> value,
            IDependencyCollection dependencies,
            bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return WriteInternal(serializationManager, value, alwaysWrite, context);
        }

        List<T> ITypeReader<List<T>, SequenceDataNode>.Read(ISerializationManager serializationManager,
            SequenceDataNode node,
            IDependencyCollection dependencies,
            bool skipHook,
            ISerializationContext? context, List<T>? list)
        {
            list ??= new List<T>();

            foreach (var dataNode in node.Sequence)
            {
                list.Add(serializationManager.Read<T>(dataNode, context, skipHook));
            }

            return list;
        }

        ValidationNode ITypeValidator<ImmutableList<T>, SequenceDataNode>.Validate(
            ISerializationManager serializationManager,
            SequenceDataNode node, IDependencyCollection dependencies, ISerializationContext? context)
        {
            return Validate(serializationManager, node, context);
        }

        ValidationNode ITypeValidator<IReadOnlyCollection<T>, SequenceDataNode>.Validate(
            ISerializationManager serializationManager,
            SequenceDataNode node, IDependencyCollection dependencies, ISerializationContext? context)
        {
            return Validate(serializationManager, node, context);
        }

        ValidationNode ITypeValidator<IReadOnlyList<T>, SequenceDataNode>.Validate(
            ISerializationManager serializationManager,
            SequenceDataNode node, IDependencyCollection dependencies, ISerializationContext? context)
        {
            return Validate(serializationManager, node, context);
        }

        ValidationNode ITypeValidator<List<T>, SequenceDataNode>.Validate(ISerializationManager serializationManager,
            SequenceDataNode node, IDependencyCollection dependencies, ISerializationContext? context)
        {
            return Validate(serializationManager, node, context);
        }

        ValidationNode Validate(ISerializationManager serializationManager, SequenceDataNode sequenceDataNode, ISerializationContext? context)
        {
            var list = new List<ValidationNode>();
            foreach (var elem in sequenceDataNode.Sequence)
            {
                list.Add(serializationManager.ValidateNode(typeof(T), elem, context));
            }

            return new ValidatedSequenceNode(list);
        }

        IReadOnlyList<T> ITypeReader<IReadOnlyList<T>, SequenceDataNode>.Read(
            ISerializationManager serializationManager, SequenceDataNode node,
            IDependencyCollection dependencies,
            bool skipHook, ISerializationContext? context, IReadOnlyList<T>? rawValue)
        {
            if(rawValue != null)
                Logger.Warning($"Provided value to a Read-call for a {nameof(IReadOnlySet<T>)}. Ignoring...");

            var list = new List<T>();

            foreach (var dataNode in node.Sequence)
            {
                list.Add(serializationManager.Read<T>(dataNode, context, skipHook));
            }

            return list;
        }

        IReadOnlyCollection<T> ITypeReader<IReadOnlyCollection<T>, SequenceDataNode>.Read(
            ISerializationManager serializationManager, SequenceDataNode node,
            IDependencyCollection dependencies,
            bool skipHook, ISerializationContext? context, IReadOnlyCollection<T>? rawValue)
        {
            if(rawValue != null)
                Logger.Warning($"Provided value to a Read-call for a {nameof(IReadOnlyCollection<T>)}. Ignoring...");

            var list = new List<T>();

            foreach (var dataNode in node.Sequence)
            {
                list.Add(serializationManager.Read<T>(dataNode, context, skipHook));
            }

            return list;
        }

        ImmutableList<T> ITypeReader<ImmutableList<T>, SequenceDataNode>.Read(
            ISerializationManager serializationManager, SequenceDataNode node,
            IDependencyCollection dependencies,
            bool skipHook, ISerializationContext? context, ImmutableList<T>? rawValue)
        {
            if(rawValue != null)
                Logger.Warning($"Provided value to a Read-call for a {nameof(ImmutableList<T>)}. Ignoring...");

            var list = ImmutableList.CreateBuilder<T>();

            foreach (var dataNode in node.Sequence)
            {
                list.Add(serializationManager.Read<T>(dataNode, context, skipHook));
            }

            return list.ToImmutable();
        }

        public void CopyTo(ISerializationManager serializationManager, List<T> source, ref List<T> target, bool skipHook,
            ISerializationContext? context = null)
        {
            target.Clear();
            foreach (var val in source)
            {
                target.Add(serializationManager.CreateCopy(val, context, skipHook));
            }
        }
    }
}

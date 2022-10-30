using System.Collections.Generic;
using System.Linq;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

namespace Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;

[TypeSerializer]
public sealed class DictionarySerializer<TKey, TValue> :
    ITypeSerializer<Dictionary<TKey, TValue>, MappingDataNode>,
    ITypeSerializer<IReadOnlyDictionary<TKey, TValue>, MappingDataNode>,
    ITypeSerializer<SortedDictionary<TKey, TValue>, MappingDataNode>,
    ITypeCopier<Dictionary<TKey, TValue>>,
    ITypeCopier<SortedDictionary<TKey, TValue>> where TKey : notnull

{
    private MappingDataNode InterfaceWrite(
        ISerializationManager serializationManager,
        IDictionary<TKey, TValue> value,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
    {
        var mappingNode = new MappingDataNode();

        foreach (var (key, val) in value)
        {
            mappingNode.Add(
                serializationManager.WriteValue(key, alwaysWrite, context),
                serializationManager.WriteValue(typeof(TValue), val, alwaysWrite, context));
        }

        return mappingNode;
    }

    public Dictionary<TKey, TValue> Read(ISerializationManager serializationManager,
        MappingDataNode node, IDependencyCollection dependencies, bool skipHook, ISerializationContext? context,
        Dictionary<TKey, TValue>? dict)
    {
        dict ??= new Dictionary<TKey, TValue>();

        foreach (var (key, value) in node.Children)
        {
            dict.Add(serializationManager.Read<TKey>(key, context, skipHook),
                serializationManager.Read<TValue>(value, context, skipHook));
        }

        return dict;
    }

    ValidationNode ITypeValidator<SortedDictionary<TKey, TValue>, MappingDataNode>.Validate(
        ISerializationManager serializationManager, MappingDataNode node, IDependencyCollection dependencies,
        ISerializationContext? context)
    {
        return Validate(serializationManager, node, context);
    }

    ValidationNode ITypeValidator<IReadOnlyDictionary<TKey, TValue>, MappingDataNode>.Validate(
        ISerializationManager serializationManager, MappingDataNode node, IDependencyCollection dependencies,
        ISerializationContext? context)
    {
        return Validate(serializationManager, node, context);
    }

    ValidationNode ITypeValidator<Dictionary<TKey, TValue>, MappingDataNode>.Validate(
        ISerializationManager serializationManager,
        MappingDataNode node, IDependencyCollection dependencies, ISerializationContext? context)
    {
        return Validate(serializationManager, node, context);
    }

    ValidationNode Validate(ISerializationManager serializationManager, MappingDataNode node,
        ISerializationContext? context)
    {
        var mapping = new Dictionary<ValidationNode, ValidationNode>();
        foreach (var (key, val) in node.Children)
        {
            mapping.Add(serializationManager.ValidateNode(typeof(TKey), key, context),
                serializationManager.ValidateNode(typeof(TValue), val, context));
        }

        return new ValidatedMappingNode(mapping);
    }

    public DataNode Write(ISerializationManager serializationManager, Dictionary<TKey, TValue> value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
    {
        return InterfaceWrite(serializationManager, value, alwaysWrite, context);
    }

    public DataNode Write(ISerializationManager serializationManager, SortedDictionary<TKey, TValue> value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
    {
        return InterfaceWrite(serializationManager, value, alwaysWrite, context);
    }

    public DataNode Write(ISerializationManager serializationManager, IReadOnlyDictionary<TKey, TValue> value,
        IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
    {
        return InterfaceWrite(serializationManager, value.ToDictionary(k => k.Key, v => v.Value), alwaysWrite, context);
    }

    IReadOnlyDictionary<TKey, TValue>
        ITypeReader<IReadOnlyDictionary<TKey, TValue>, MappingDataNode>.Read(
            ISerializationManager serializationManager, MappingDataNode node,
            IDependencyCollection dependencies,
            bool skipHook, ISerializationContext? context, IReadOnlyDictionary<TKey, TValue>? rawValue)
    {
        if (rawValue != null)
        {
            Logger.Warning(
                $"Provided value to a Read-call for a {nameof(IReadOnlyDictionary<TKey, TValue>)}. Ignoring...");
        }

        var dict = new Dictionary<TKey, TValue>();

        foreach (var (key, value) in node.Children)
        {
            dict.Add(serializationManager.Read<TKey>(key, context, skipHook),
                serializationManager.Read<TValue>(value, context, skipHook));
        }

        return dict;
    }

    SortedDictionary<TKey, TValue>
        ITypeReader<SortedDictionary<TKey, TValue>, MappingDataNode>.Read(
            ISerializationManager serializationManager, MappingDataNode node,
            IDependencyCollection dependencies,
            bool skipHook, ISerializationContext? context, SortedDictionary<TKey, TValue>? dict)
    {
        dict ??= new SortedDictionary<TKey, TValue>();

        foreach (var (key, value) in node.Children)
        {
            dict.Add(serializationManager.Read<TKey>(key, context, skipHook),
                serializationManager.Read<TValue>(value, context, skipHook));
        }

        return dict;
    }

    public void CopyTo(ISerializationManager serializationManager, Dictionary<TKey, TValue> source, ref Dictionary<TKey, TValue> target, bool skipHook,
        ISerializationContext? context = null)
    {
        target.Clear();
        foreach (var value in source)
        {
            target.Add(
                serializationManager.CreateCopy(value.Key, context, skipHook),
                serializationManager.CreateCopy(value.Value, context, skipHook));
        }
    }

    public void CopyTo(ISerializationManager serializationManager, SortedDictionary<TKey, TValue> source, ref SortedDictionary<TKey, TValue> target,
        bool skipHook, ISerializationContext? context = null)
    {
        target.Clear();
        foreach (var value in source)
        {
            target.Add(
                serializationManager.CreateCopy(value.Key, context, skipHook),
                serializationManager.CreateCopy(value.Value, context, skipHook));
        }
    }
}

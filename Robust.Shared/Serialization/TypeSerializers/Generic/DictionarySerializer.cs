using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Robust.Shared.IoC;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;

namespace Robust.Shared.Serialization.TypeSerializers.Generic
{
    [TypeSerializer]
    public class DictionarySerializer<TKey, TValue> :
        ITypeSerializer<Dictionary<TKey, TValue>, MappingDataNode>,
        ITypeSerializer<IReadOnlyDictionary<TKey, TValue>, MappingDataNode>,
        ITypeSerializer<SortedDictionary<TKey, TValue>, MappingDataNode>
        where TKey : notnull
    {
        [Dependency] private readonly ISerializationManager _serializationManager = default!;

        private MappingDataNode InterfaceWrite(
            IDictionary<TKey, TValue> value,
            bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            var mappingNode = new MappingDataNode();

            foreach (var (key, val) in value)
            {
                mappingNode.AddNode(
                    _serializationManager.WriteValue(key, alwaysWrite, context),
                    _serializationManager.WriteValue(val, alwaysWrite, context));
            }

            return mappingNode;
        }

        private Dictionary<TKey, TValue> NormalRead(MappingDataNode node, ISerializationContext? context = null)
        {
            var dict = new Dictionary<TKey, TValue>();

            foreach (var (key, value) in node.Children)
            {
                var keyValue = _serializationManager.ReadValue<TKey>(key, context);

                var valueValue = _serializationManager.ReadValue<TValue>(value, context);
                dict.Add(keyValue, valueValue);
            }

            return dict;
        }

        public Dictionary<TKey, TValue> Read(MappingDataNode node, ISerializationContext? context)
        {
            return NormalRead(node, context);
        }

        public DataNode Write(Dictionary<TKey, TValue> value, bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return InterfaceWrite(value, alwaysWrite, context);
        }

        public DataNode Write(SortedDictionary<TKey, TValue> value, bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return InterfaceWrite(value, alwaysWrite, context);
        }

        public DataNode Write(IReadOnlyDictionary<TKey, TValue> value, bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            return InterfaceWrite(value.ToDictionary(k => k.Key, v => v.Value), alwaysWrite, context);
        }

        IReadOnlyDictionary<TKey, TValue> ITypeReader<IReadOnlyDictionary<TKey, TValue>, MappingDataNode>.Read(MappingDataNode node, ISerializationContext? context)
        {
            return NormalRead(node, context);
        }

        SortedDictionary<TKey, TValue> ITypeReader<SortedDictionary<TKey, TValue>, MappingDataNode>.Read(MappingDataNode node, ISerializationContext? context)
        {
            return new(NormalRead(node, context));
        }

        [MustUseReturnValue]
        private T CopyInternal<T>(IReadOnlyDictionary<TKey, TValue> source, T target) where T : IDictionary<TKey, TValue>
        {
            target.Clear();

            foreach (var (key, value) in source)
            {
                var keyCopy = _serializationManager.CreateCopy(key) ?? throw new NullReferenceException();
                var valueCopy = _serializationManager.CreateCopy(value)!;

                target.Add(keyCopy, valueCopy);
            }

            return target;
        }

        [MustUseReturnValue]
        public Dictionary<TKey, TValue> Copy(Dictionary<TKey, TValue> source, Dictionary<TKey, TValue> target)
        {
            return CopyInternal(source, target);
        }

        [MustUseReturnValue]
        public IReadOnlyDictionary<TKey, TValue> Copy(IReadOnlyDictionary<TKey, TValue> source, IReadOnlyDictionary<TKey, TValue> target)
        {
            if (target is Dictionary<TKey, TValue> targetDictionary)
            {
                return CopyInternal(source, targetDictionary);
            }

            var dictionary = new Dictionary<TKey, TValue>(source.Count);

            foreach (var (key, value) in source)
            {
                var keyCopy = _serializationManager.CreateCopy(key) ?? throw new NullReferenceException();
                var valueCopy = _serializationManager.CreateCopy(value)!;

                dictionary.Add(keyCopy, valueCopy);
            }

            return dictionary;
        }

        [MustUseReturnValue]
        public SortedDictionary<TKey, TValue> Copy(SortedDictionary<TKey, TValue> source, SortedDictionary<TKey, TValue> target)
        {
            return CopyInternal(source, target);
        }
    }
}

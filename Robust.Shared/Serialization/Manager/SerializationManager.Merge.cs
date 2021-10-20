using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using Robust.Shared.Serialization.Manager.Definition;
using Robust.Shared.Serialization.Manager.Result;

namespace Robust.Shared.Serialization.Manager
{
    public partial class SerializationManager
    {
        private delegate void MergeDelegate(
            object obj,
            DeserializationResult deserialization,
            bool skipHook = false);

        private readonly ConcurrentDictionary<Type, MergeDelegate?> _mergers = new();

        public void MergePopulate(ref object? obj, DeserializationResult deserialization)
        {
            var merger = obj == null ? null : GetOrCreateMerger(obj.GetType());
            if (merger == null)
            {
                obj = deserialization.RawValue;
                return;
            }

            merger.Invoke(obj!, deserialization);
        }

        private MergeDelegate? GetOrCreateMerger(Type objType)
        {
            return _mergers.GetOrAdd(objType, static (type, instance) =>
            {
                var instanceConst = Expression.Constant(instance);
                var objectParam = Expression.Parameter(typeof(object), "obj");
                var deserializationParam = Expression.Parameter(typeof(DeserializationResult), "deserialization");
                var skipHookParam = Expression.Parameter(typeof(bool), "skipHook");

                Expression call;

                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    var genericArguments = type.GetGenericArguments();
                    call = Expression.Call(
                        instanceConst,
                        nameof(MergeDictionary),
                        genericArguments,
                        Expression.Convert(objectParam, typeof(IDictionary<,>).MakeGenericType(genericArguments)),
                        Expression.Convert(deserializationParam, typeof(DeserializedDictionary<,,>).MakeGenericType(
                            typeof(Dictionary<,>).MakeGenericType(genericArguments),
                            genericArguments[0],
                            genericArguments[1]))
                    );
                }
                else if (instance.TryGetDefinition(type, out var definition))
                {
                    var definitionConst = Expression.Constant(definition, typeof(DataDefinition));

                    call = Expression.Call(
                        instanceConst,
                        nameof(MergeDataDefinition),
                        Array.Empty<Type>(),
                        objectParam,
                        Expression.Convert(deserializationParam, typeof(IDeserializedDefinition)),
                        definitionConst);
                }
                else
                {
                    return null;
                }

                return Expression.Lambda<MergeDelegate>(
                    call,
                    objectParam,
                    deserializationParam,
                    skipHookParam).Compile();
            }, this);
        }

        private void MergeDictionary<TKey, TValue>(
            IDictionary<TKey, TValue> target,
            DeserializedDictionary<Dictionary<TKey, TValue>, TKey, TValue> result)
            where TKey : notnull
        {
            foreach (var (k, v) in result.Mappings)
            {
                var kValue = (TKey) k.RawValue!;
                if (!typeof(TValue).IsValueType && target.TryGetValue(kValue, out var targetV) && targetV != null)
                {
                    // Merge
                    object? objValue = targetV;
                    MergePopulate(ref objValue, v);
                    target[kValue] = (TValue) objValue!;
                }
                else
                {
                    // Populate
                    target[kValue] = (TValue) v.RawValue!;
                }
            }
        }

        private DeserializationResult MergeDataDefinition(
            object target,
            IDeserializedDefinition deserializedDefinition,
            DataDefinition definition)
        {

            return definition.Populate(target, deserializedDefinition.Mapping, this, merging: true);
            /*var type = typeof(TValue);
            var instance = instantiator();

            if (context != null &&
                context.TypeReaders.TryGetValue((type, typeof(MappingDataNode)), out var readerUnCast))
            {
                var reader = (ITypeReader<TValue, MappingDataNode>)readerUnCast;
                return reader.Read(this, node, DependencyCollection, skipHook, context);
            }

            if (definition == null)
            {
                throw new ArgumentException(
                    $"No data definition found for type {type} with node type {node.GetType()} when reading");
            }

            if (populate)
            {
                ((IPopulateDefaultValues)instance).PopulateDefaultValues();
            }

            var result = definition.Populate(instance, node, this, context, skipHook);

            if (!skipHook && hooks)
            {
                ((ISerializationHooks)result.RawValue!).AfterDeserialization();
            }*/
        }
    }
}

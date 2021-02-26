using System;
using Robust.Shared.IoC;

namespace Robust.Shared.Serialization.Manager.Result
{
    public abstract class DeserializationResult
    {
        public abstract object? RawValue { get; }

        public abstract DeserializationResult PushInheritanceFrom(DeserializationResult source);
        public abstract DeserializationResult Copy();

        public static DeserializedValue<T> Value<T>(T? value)
        {
            var type = typeof(DeserializedValue<>).MakeGenericType(typeof(T));
            return (DeserializedValue<T>) Activator.CreateInstance(type, value)!;
        }

        public static DeserializationResult Definition(object value, DeserializedFieldEntry[] mappings)
        {
            if (!IoCManager.Resolve<ISerializationManager>().HasDataDefinition(value.GetType()))
                throw new ArgumentException("Provided value was not a datadefinition", nameof(value));

            //todo validate mappings array count
            var type = typeof(DeserializedDefinition<>).MakeGenericType(value.GetType());
            return (DeserializationResult) Activator.CreateInstance(type, value, mappings)!;
        }

        public T As<T>() where T : DeserializationResult
        {
            if (this is T value) return value;
            throw new InvalidDeserializedResultTypeException<T>(GetType());
        }
    }

    public abstract class DeserializationResult<T> : DeserializationResult
    {
        public abstract T Value { get; }

        public T ValueOrThrow => Value ?? throw new NullReferenceException();
    }
}

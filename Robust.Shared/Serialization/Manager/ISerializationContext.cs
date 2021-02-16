using System;
using System.Collections.Generic;

namespace Robust.Shared.Serialization.Manager
{
    public interface ISerializationContext
    {
        Dictionary<Type, YamlObjectSerializer.TypeSerializer> TypeSerializers { get; }
    }
}

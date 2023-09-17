﻿using Robust.Shared.IoC;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;
using Robust.Shared.Utility;

namespace Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Percents;

public sealed class PercentageSerializer : ITypeSerializer<float, ValueDataNode>
{
    public ValidationNode Validate(ISerializationManager serializationManager, ValueDataNode node,
        IDependencyCollection dependencies, ISerializationContext? context = null)
    {
        return PercentageSerializerUtility.TryParse(node.Value, out _)
            ? new ValidatedValueNode(node)
            : new ErrorNode(node, "Failed parsing values for percentage");
    }

    public float Read(ISerializationManager serializationManager, ValueDataNode node,
        IDependencyCollection dependencies, SerializationHookContext hookCtx, ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<float>? instanceProvider = null)
    {
        if (!PercentageSerializerUtility.TryParse(node.Value, out var @float))
            throw new InvalidMappingException("Could not parse percentage");

        return @float.Value;
    }

    public DataNode Write(ISerializationManager serializationManager, float value, IDependencyCollection dependencies,
        bool alwaysWrite = false,
        ISerializationContext? context = null)
    {
        return new ValueDataNode($"{value * 100}%");
    }
}

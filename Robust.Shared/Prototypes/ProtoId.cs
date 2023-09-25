﻿using System;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Generic;

namespace Robust.Shared.Prototypes;

/// <summary>
///     Wrapper type for a prototype id of kind <see cref="T"/>.
/// </summary>
/// <param name="Id">The id of the prototype.</param>
/// <typeparam name="T">The kind of prototype to wrap, for example <see cref="TileAliasPrototype"/></typeparam>
/// <remarks>
///     This will be automatically validated by <see cref="ProtoIdSerializer{T}"/> if used in data fields.
/// </remarks>
/// <remarks><seealso cref="EntProtoId"/> for an <see cref="EntityPrototype"/> alias.</remarks>
[Serializable]
public readonly record struct ProtoId<T>(string Id) : IEquatable<string> where T : class, IPrototype
{
    public static implicit operator string(ProtoId<T> protoId)
    {
        return protoId.Id;
    }

    public static implicit operator ProtoId<T>(string id)
    {
        return new ProtoId<T>(id);
    }

    public static implicit operator ProtoId<T>?(string? id)
    {
        return id == null ? default(ProtoId<T>?) : new ProtoId<T>(id);
    }

    public static bool operator ==(ProtoId<T>? first, string? second)
    {
        if (!first.HasValue)
            return second == null;

        return first.Value.Equals(second);
    }

    public static bool operator !=(ProtoId<T>? first, string? second)
    {
        return !(first == second);
    }

    public static bool operator ==(ProtoId<T> first, string? second)
    {
        return first.Id == second;
    }

    public static bool operator !=(ProtoId<T> first, string? second)
    {
        return !(first == second);
    }

    public bool Equals(string? other)
    {
        return Id == other;
    }
}

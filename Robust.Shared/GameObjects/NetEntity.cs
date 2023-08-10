using System;
using JetBrains.Annotations;
using Robust.Shared.Serialization;

namespace Robust.Shared.GameObjects;

/// <summary>
/// Network identifier for entities; used by client and server to refer to the same entity where their local <see cref="EntityUid"/> may differ.
/// </summary>
[Serializable, NetSerializable]
public struct NetEntity : IEquatable<NetEntity>, IComparable<NetEntity>, ISpanFormattable
{
    private readonly int _id;

    /*
     * Differed to EntityUid to be more consistent with Arch.
     */

    /// <summary>
    ///     An Invalid entity UID you can compare against.
    /// </summary>
    public static readonly NetEntity Invalid = new(-1);

    /// <summary>
    ///     The first entity UID the entityManager should use when the manager is initialized.
    /// </summary>
    public static readonly NetEntity First = new(0);

    /// <summary>
    ///     Creates an instance of this structure, with the given network ID.
    /// </summary>
    public NetEntity(int id)
    {
        _id = id;
    }

    public bool Valid => IsValid();

    /// <summary>
    ///     Creates a network entity UID by parsing a string number.
    /// </summary>
    public static NetEntity Parse(ReadOnlySpan<char> uid)
    {
        return new NetEntity(int.Parse(uid));
    }

    public static bool TryParse(ReadOnlySpan<char> uid, out NetEntity entity)
    {
        try
        {
            entity = Parse(uid);
            return true;
        }
        catch (FormatException)
        {
            entity = Invalid;
            return false;
        }
    }

    /// <summary>
    ///     Checks if the ID value is valid. Does not check if it identifies
    ///     a valid Entity.
    /// </summary>
    [Pure]
    public bool IsValid()
    {
        return _id > -1;
    }

    /// <inheritdoc />
    public bool Equals(NetEntity other)
    {
        return _id == other._id;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        return obj is EntityUid id && Equals(id);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return _id;
    }

    /// <summary>
    ///     Check for equality by value between two objects.
    /// </summary>
    public static bool operator ==(NetEntity a, NetEntity b)
    {
        return a._id == b._id;
    }

    /// <summary>
    ///     Check for inequality by value between two objects.
    /// </summary>
    public static bool operator !=(NetEntity a, NetEntity b)
    {
        return !(a == b);
    }

    /// <summary>
    ///     Explicit conversion of EntityId to int. This should only be used in special
    ///     cases like serialization. Do NOT use this in content.
    /// </summary>
    public static explicit operator int(NetEntity self)
    {
        return self._id;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return _id.ToString();
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return ToString();
    }

    public bool TryFormat(
        Span<char> destination,
        out int charsWritten,
        ReadOnlySpan<char> format,
        IFormatProvider? provider)
    {
        return _id.TryFormat(destination, out charsWritten);
    }

    /// <inheritdoc />
    public int CompareTo(NetEntity other)
    {
        return _id.CompareTo(other._id);
    }
}
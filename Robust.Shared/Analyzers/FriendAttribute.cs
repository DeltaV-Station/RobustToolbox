using System;

namespace Robust.Shared.Analyzers;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct
                | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Constructor)]
public sealed class FriendAttribute : Attribute
{
    public readonly Type[] Friends;

    public const AccessPermissions SelfDefaultPermissions = AccessPermissions.ReadWriteExecute;
    public const AccessPermissions FriendDefaultPermissions = AccessPermissions.ReadWriteExecute;
    public const AccessPermissions OtherDefaultPermissions = AccessPermissions.Read;

    /// <summary>
    ///     Access permissions for the type itself, or the type containing the member.
    /// </summary>
    public AccessPermissions Self   { get; set; }  = SelfDefaultPermissions;
    public AccessPermissions Friend { get; set; }  = FriendDefaultPermissions;
    public AccessPermissions Other  { get; set;  } = OtherDefaultPermissions;

    public FriendAttribute(params Type[] friends)
    {
        Friends = friends;
    }
}

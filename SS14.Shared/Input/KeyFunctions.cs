﻿using System;
using SS14.Shared.Serialization;

namespace SS14.Shared.Input
{
    public enum BoundKeyState : byte
    {
        Up = 0,
        Down = 1
    }

    [KeyFunctions]
    public static class EngineKeyFunctions
    {
        public static readonly BoundKeyFunction MoveUp = "MoveUp";
        public static readonly BoundKeyFunction MoveDown = "MoveDown";
        public static readonly BoundKeyFunction MoveLeft = "MoveLeft";
        public static readonly BoundKeyFunction MoveRight = "MoveRight";
        public static readonly BoundKeyFunction Run = "Run";

        public static readonly BoundKeyFunction ShowDebugMonitors = "ShowDebugMonitors";
        public static readonly BoundKeyFunction EscapeMenu = "ShowEscapeMenu";
        public static readonly BoundKeyFunction FocusChat = "FocusChatWindow";

        public static readonly BoundKeyFunction EditorLinePlace = "EditorLinePlace";
        public static readonly BoundKeyFunction EditorGridPlace = "EditorGridPlace";
        public static readonly BoundKeyFunction EditorPlaceObject = "EditorPlaceObject";
        public static readonly BoundKeyFunction EditorCancelPlace = "EditorCancelPlace";
        public static readonly BoundKeyFunction EditorRotateObject = "EditorRotateObject";
    }

    [Serializable, NetSerializable]
    public struct BoundKeyFunction : IComparable, IComparable<BoundKeyFunction>, IEquatable<BoundKeyFunction>
    {
        public readonly string FunctionName;

        public BoundKeyFunction(string name)
        {
            FunctionName = name;
        }

        public static implicit operator BoundKeyFunction(string name)
        {
            return new BoundKeyFunction(name);
        }

        public override string ToString()
        {
            return $"KeyFunction({FunctionName})";
        }

        #region Code for easy equality and sorting.

        public int CompareTo(object obj)
        {
            return CompareTo((BoundKeyFunction) obj);
        }

        public int CompareTo(BoundKeyFunction other)
        {
            return string.Compare(FunctionName, other.FunctionName, StringComparison.InvariantCultureIgnoreCase);
        }

        // Could maybe go dirty and optimize these on the assumption that they're singletons.
        public override bool Equals(object obj)
        {
            return Equals((BoundKeyFunction) obj);
        }

        public bool Equals(BoundKeyFunction other)
        {
            return other.FunctionName == FunctionName;
        }

        public override int GetHashCode()
        {
            return FunctionName.GetHashCode();
        }

        public static bool operator ==(BoundKeyFunction a, BoundKeyFunction b)
        {
            return a.FunctionName == b.FunctionName;
        }

        public static bool operator !=(BoundKeyFunction a, BoundKeyFunction b)
        {
            return !(a == b);
        }

        #endregion
    }

    /// <summary>
    ///     Makes all constant strings on this static class be added as input functions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class KeyFunctionsAttribute : Attribute { }
}

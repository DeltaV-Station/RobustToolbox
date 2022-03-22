﻿using System;
using JetBrains.Annotations;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;
using Robust.Shared.ViewVariables;
using YamlDotNet.RepresentationModel;

namespace Robust.Shared.Prototypes
{
    /// <summary>
    ///     An IPrototype is a prototype that can be loaded from the global YAML prototypes.
    /// </summary>
    /// <remarks>
    ///     To use this, the prototype must be accessible through IoC with <see cref="IoCTargetAttribute"/>
    ///     and it must have a <see cref="PrototypeAttribute"/> to give it a type string.
    /// </remarks>
    public interface IPrototype
    {
        /// <summary>
        /// An ID for this prototype instance.
        /// If this is a duplicate, an error will be thrown.
        /// </summary>
        [ViewVariables(VVAccess.ReadOnly)] string ID { get; }
    }

    public interface IInheritingPrototype
    {
        string? Parent { get; }

        bool Abstract { get; }
    }

    public sealed class IdDataFieldAttribute : DataFieldAttribute
    {
        public IdDataFieldAttribute(int priority = 1, Type? customTypeSerializer = null) :
            base("id", false, priority, true, false, customTypeSerializer)
        {
        }
    }

    public sealed class ParentDataFieldAttribute : DataFieldAttribute
    {
        public ParentDataFieldAttribute(Type prototypeIdSerializer, int priority = 1) :
            base("parent", false, priority, false, false, prototypeIdSerializer)
        {
        }
    }
}

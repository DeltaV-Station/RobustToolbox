﻿using System;
using System.Collections.Generic;
using System.Globalization;
using Fluent.Net;
using JetBrains.Annotations;

namespace Robust.Shared.Localization
{
    // Basically Fluent.Net is based on an ancient pre-1.0 version of fluent.js.
    // Said version of fluent.js was also a complete garbage fire implementation wise.
    // So Fluent.Net is garbage implementation wise.
    // ...yay
    // (the current implementation of fluent.js is written in TS and actually sane)
    //
    // Because of this, we can't expose it to content, so we have to wrap everything related to functions.
    // This basically mimics the modern typescript fluent.js API. Somewhat.

    /// <summary>
    ///     Function signature runnable by localizations.
    /// </summary>
    /// <param name="args">Contains arguments and options passed to the function by the calling localization.</param>
    public delegate ILocValue LocFunction(LocArgs args);

    [PublicAPI]
    public readonly struct LocContext
    {
        public CultureInfo Culture => Context.Culture;

        internal readonly MessageContext Context;

        internal LocContext(MessageContext ctx)
        {
            Context = ctx;
        }
    }

    /// <summary>
    ///     Arguments and options passed to a localization function.
    /// </summary>
    [PublicAPI]
    public readonly struct LocArgs
    {
        public LocArgs(IReadOnlyList<ILocValue> args, IReadOnlyDictionary<string, ILocValue> options)
        {
            Args = args;
            Options = options;
        }

        /// <summary>
        ///     Positional arguments passed to the function, in order.
        /// </summary>
        public IReadOnlyList<ILocValue> Args { get; }

        /// <summary>
        ///     Key-value options passed to the function.
        /// </summary>
        public IReadOnlyDictionary<string, ILocValue> Options { get; }
    }

    /// <summary>
    ///     A value passed around in the localization system.
    /// </summary>
    /// <seealso cref="LocValue{T}"/>
    public interface ILocValue
    {
        /// <summary>
        ///     Format this value to a string.
        /// </summary>
        /// <remarks>
        ///     Used when this value is interpolated directly in localizations.
        /// </remarks>
        /// <param name="ctx">Context containing data like culture used.</param>
        /// <returns>The formatted string.</returns>
        string Format(LocContext ctx);

        /// <summary>
        ///     Boxed value stored by this instance.
        /// </summary>
        object? Value { get; }
    }

    /// <summary>
    ///     Default implementation of a localization value.
    /// </summary>
    /// <remarks>
    ///     The idea is that inheritors could add extra data like formatting parameters
    ///     and then use those by overriding <see cref="Format"/>.
    /// </remarks>
    /// <typeparam name="T">The type of value stored.</typeparam>
    [PublicAPI]
    public abstract record LocValue<T> : ILocValue
    {
        /// <summary>
        ///     The stored value.
        /// </summary>
        public T Value { get; init; }

        object? ILocValue.Value => Value;

        protected LocValue(T val)
        {
            Value = val;
        }

        public abstract string Format(LocContext ctx);
    }

    public sealed record LocValueNumber(double Value) : LocValue<double>(Value)
    {
        public override string Format(LocContext ctx)
        {
            return Value.ToString(ctx.Culture);
        }
    }

    public sealed record LocValueDateTime(DateTime Value) : LocValue<DateTime>(Value)
    {
        public override string Format(LocContext ctx)
        {
            return Value.ToString(ctx.Culture);
        }
    }

    public sealed record LocValueString(string Value) : LocValue<string>(Value)
    {
        public override string Format(LocContext ctx)
        {
            return Value;
        }
    }

    /// <summary>
    ///     Stores an "invalid" string value. Produced by e.g. unresolved variable references.
    /// </summary>
    public sealed record LocValueNone(string Value) : LocValue<string>(Value)
    {
        public override string Format(LocContext ctx)
        {
            return Value;
        }
    }
}

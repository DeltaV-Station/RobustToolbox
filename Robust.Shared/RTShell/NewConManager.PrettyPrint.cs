﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Utility;

namespace Robust.Shared.RTShell;

public sealed partial class NewConManager
{
    public string PrettyPrintType(object? value)
    {
        if (value is null)
            return "null";

        if (value is string str)
            return str;

        if (value is FormattedMessage msg)
            return msg.ToMarkup();

        if (value is EntityUid uid)
        {
            return _entity.ToPrettyString(uid);
        }

        if (value is Type t)
        {
            return t.PrettyName();
        }

        if (value.GetType().IsAssignableTo(typeof(IEnumerable<EntityUid>)))
        {
            return string.Join(",\n", ((IEnumerable<EntityUid>) value).Select(_entity.ToPrettyString));
        }

        if (value.GetType().IsAssignableTo(typeof(IEnumerable)))
        {
            return string.Join(",\n", ((IEnumerable) value).Cast<object?>().Select(PrettyPrintType));
        }

        if (value.GetType().IsAssignableTo(typeof(IDictionary)))
        {
            var dict = ((IDictionary) value).GetEnumerator();

            var kvList = new List<string>();

            do
            {
                kvList.Add($"({PrettyPrintType(dict.Key)}, {PrettyPrintType(dict.Value)}");
            } while (dict.MoveNext());

            return $"Dictionary {{\n{string.Join(",\n", kvList)}\n}}";
        }

        return value.ToString() ?? "[unrepresentable]";
    }
}

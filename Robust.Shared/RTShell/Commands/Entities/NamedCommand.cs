﻿using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Robust.Shared.GameObjects;

namespace Robust.Shared.RTShell.Commands.Entities;

[RtShellCommand]
public sealed class NamedCommand : RtShellCommand
{
    [CommandImplementation]
    public IEnumerable<EntityUid> Named([PipedArgument] IEnumerable<EntityUid> input, [CommandArgument] string regex, [CommandInverted] bool inverted)
    {
        var compiled = new Regex($"${regex}^");
        return input.Where(x => compiled.IsMatch(EntName(x)) ^ inverted);
    }
}

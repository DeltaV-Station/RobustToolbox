﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Robust.Shared.RTShell.Commands.Generic;

[RtShellCommand]
internal sealed class CountCommand : RtShellCommand
{
    [CommandImplementation, TakesPipedTypeAsGeneric]
    public int Count<T>([PipedArgument] IEnumerable<T> enumerable)
    {
        return enumerable.Count();
    }
}

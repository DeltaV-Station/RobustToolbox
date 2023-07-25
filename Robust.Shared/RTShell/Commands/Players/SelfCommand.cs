﻿using Robust.Shared.GameObjects;
using Robust.Shared.RTShell.Errors;

namespace Robust.Shared.RTShell.Commands.Players;

[ConsoleCommand]
public sealed class SelfCommand : ConsoleCommand
{
    [CommandImplementation]
    public EntityUid Self([CommandInvocationContext] IInvocationContext ctx)
    {
        if (ctx.Session is null)
        {
            ctx.ReportError(new NotForServerConsoleError());
            return default!;
        }

        if (ctx.Session.AttachedEntity is { } ent)
            return ent;

        ctx.ReportError(new SessionHasNoEntityError(ctx.Session));
        return default!;
    }
}


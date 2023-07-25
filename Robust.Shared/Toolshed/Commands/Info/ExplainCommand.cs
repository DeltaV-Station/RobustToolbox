﻿using Robust.Shared.Toolshed.Syntax;

namespace Robust.Shared.Toolshed.Commands.Info;

[ToolshedCommand]
public sealed class ExplainCommand : ToolshedCommand
{
    [CommandImplementation]
    public void Explain(
        [CommandInvocationContext] IInvocationContext ctx,
        [CommandArgument] CommandRun expr
    )
    {
        foreach (var (cmd, span) in expr.Commands)
        {
            ctx.WriteLine(cmd.Command.GetHelp(cmd.SubCommand));
            ctx.WriteLine($"{cmd.PipedType?.PrettyName() ?? "[none]"} -> {cmd.ReturnType?.PrettyName() ?? "[none]"}");
        }
    }
}

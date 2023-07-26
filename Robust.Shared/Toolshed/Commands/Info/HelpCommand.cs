﻿using Robust.Shared.Maths;

namespace Robust.Shared.Toolshed.Commands.Info;

[ToolshedCommand]
public sealed class HelpCommand : ToolshedCommand
{
    private static readonly string Gold = Color.Gold.ToHex();
    private static readonly string Aqua = Color.Aqua.ToHex();

    [CommandImplementation]
    public void Help([CommandInvocationContext] IInvocationContext ctx)
    {
        ctx.WriteLine($@"
    TOOLSHED
   /.\\\\\\\\
  /___\\\\\\\\
  |''''|'''''|
  | 8  | === |
  |_0__|_____|");
        ctx.WriteMarkup($@"
For a list of commands, run [color={Gold}]cmd:list[/color].
To search for commands, run [color={Gold}]cmd:list search ""[color={Aqua}]query[/color]""[/color].
");
    }
}

﻿using Robust.Shared.Utility;

namespace Robust.Shared.Toolshed.Commands.Vfs;

[ToolshedCommand]
public sealed class CdCommand : VfsCommand
{
    [CommandImplementation]
    public void Cd(
        [CommandInvocationContext] IInvocationContext ctx,
        [CommandArgument] ResPath path
        )
    {
        var curPath = CurrentPath(ctx);

        if (path.IsRooted)
        {
            curPath = path;
        }
        else
        {
            curPath /= path;
        }

        SetPath(ctx, curPath);
    }
}

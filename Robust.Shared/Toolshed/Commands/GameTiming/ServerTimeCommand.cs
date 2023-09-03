﻿using System;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Robust.Shared.Toolshed.Commands.GameTiming;

[ToolshedCommand]
public sealed partial class ServerTimeCommand : ToolshedCommand
{
    [Dependency] private IGameTiming _gameTiming = default!;

    [CommandImplementation]
    public TimeSpan CurTime() => _gameTiming.ServerTime;
}

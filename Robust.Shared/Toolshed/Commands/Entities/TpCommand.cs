﻿using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Toolshed.Syntax;

namespace Robust.Shared.Toolshed.Commands.Entities;

[ToolshedCommand]
internal sealed class TpCommand : ToolshedCommand
{
    [CommandImplementation("coords")]
    public EntityUid TpCoords(
            [CommandInvocationContext] IInvocationContext ctx,
            [PipedArgument] EntityUid teleporter,
            [CommandArgument] ValueRef<EntityCoordinates> target
        )
    {
        Transform(teleporter).Coordinates = target.Evaluate(ctx);
        return teleporter;
    }

    [CommandImplementation("coords")]
    public IEnumerable<EntityUid> TpCoords(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> teleporters,
        [CommandArgument] ValueRef<EntityCoordinates> target
    )
        => teleporters.Select(x => TpCoords(ctx, x, target));

    [CommandImplementation("to")]
    public EntityUid TpTo(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid teleporter,
        [CommandArgument] ValueRef<EntityUid> target
    )
    {
        Transform(teleporter).Coordinates = Transform(target.Evaluate(ctx)).Coordinates;
        return teleporter;
    }

    [CommandImplementation("to")]
    public IEnumerable<EntityUid> TpTo(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> teleporters,
        [CommandArgument] ValueRef<EntityUid> target
    )
        => teleporters.Select(x => TpTo(ctx, x, target));

    [CommandImplementation("into")]
    public EntityUid TpInto(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] EntityUid teleporter,
        [CommandArgument] ValueRef<EntityUid> target
    )
    {
        Transform(teleporter).Coordinates = new EntityCoordinates(target.Evaluate(ctx), Vector2.Zero);
        return teleporter;
    }

    [CommandImplementation("into")]
    public IEnumerable<EntityUid> TpInto(
        [CommandInvocationContext] IInvocationContext ctx,
        [PipedArgument] IEnumerable<EntityUid> teleporters,
        [CommandArgument] ValueRef<EntityUid> target
    )
        => teleporters.Select(x => TpInto(ctx, x, target));
}

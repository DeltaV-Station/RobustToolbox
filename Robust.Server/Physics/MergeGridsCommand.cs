using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Robust.Server.Physics;

public sealed class MergeGridsCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "merge_grids";
    public string Description => $"Combines 2 grids into 1 grid";
    public string Help => $"{Command} <gridUid1> <gridUid2> <offsetX> <offsetY> [rotation]";
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 4)
        {
            return;
        }

        if (!EntityUid.TryParse(args[0], out var gridUidA) ||
            !EntityUid.TryParse(args[1], out var gridUidB) ||
            !_entManager.TryGetComponent<MapGridComponent>(gridUidA, out var gridA) ||
            !_entManager.TryGetComponent<MapGridComponent>(gridUidB, out var gridB))
        {
            return;
        }

        if (!int.TryParse(args[2], out var x) ||
            !int.TryParse(args[3], out var y))
        {
            return;
        }

        Angle rotation = Angle.Zero;

        if (args.Length >= 5 && int.TryParse(args[4], out var rotationInt))
        {
            rotation = Angle.FromDegrees(rotationInt);
        }

        var offset = new Vector2i(x, y);
        var fixtureSystem = _entManager.System<GridFixtureSystem>();
        fixtureSystem.Merge(gridUidA, gridUidB, offset, rotation, gridA: gridA, gridB: gridB);
    }
}

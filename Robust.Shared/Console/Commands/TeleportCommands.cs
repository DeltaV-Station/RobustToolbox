using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Players;
using Robust.Shared.Utility;

namespace Robust.Shared.Console.Commands;

internal sealed partial class TeleportCommand : LocalizedCommands
{
    [Dependency] private IMapManager _map = default!;
    [Dependency] private IEntitySystemManager _entitySystem = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    public override string Command => "tp";
    public override bool RequireServerOrSingleplayer => true;

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { AttachedEntity: { } entity })
            return;

        if (args.Length < 2 || !float.TryParse(args[0], out var posX) || !float.TryParse(args[1], out var posY))
        {
            shell.WriteError(Help);
            return;
        }

        var xformSystem = _entitySystem.GetEntitySystem<SharedTransformSystem>();
        var transform = _entityManager.GetComponent<TransformComponent>(entity);
        var position = new Vector2(posX, posY);

        xformSystem.AttachToGridOrMap(entity, transform);

        MapId mapId;
        if (args.Length == 3 && int.TryParse(args[2], out var intMapId))
            mapId = new MapId(intMapId);
        else
            mapId = transform.MapID;

        if (!_map.MapExists(mapId))
        {
            shell.WriteError($"Map {mapId} doesn't exist!");
            return;
        }

        if (_map.TryFindGridAt(mapId, position, out var gridUid, out var grid))
        {
            var gridPos = xformSystem.GetInvWorldMatrix(gridUid).Transform(position);

            xformSystem.SetCoordinates(entity, transform, new EntityCoordinates(gridUid, gridPos));
        }
        else
        {
            var mapEnt = _map.GetMapEntityIdOrThrow(mapId);
            xformSystem.SetWorldPosition(transform, position);
            xformSystem.SetParent(entity, transform, mapEnt);
        }

        shell.WriteLine($"Teleported {shell.Player} to {mapId}:{posX},{posY}.");
    }
}

public sealed partial class TeleportToCommand : LocalizedCommands
{
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private IEntityManager _entities = default!;

    public override string Command => "tpto";
    public override bool RequireServerOrSingleplayer => true;

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
            return;

        var target = args[0];

        if (!TryGetTransformFromUidOrUsername(target, shell, out _, out var targetTransform))
            return;

        var transformSystem = _entities.System<SharedTransformSystem>();
        var targetCoords = targetTransform.Coordinates;

        if (args.Length == 1)
        {
            var ent = shell.Player?.AttachedEntity;

            if (!_entities.TryGetComponent(ent, out TransformComponent? playerTransform))
            {
                shell.WriteError(Loc.GetString("cmd-failure-no-attached-entity"));
                return;
            }

            transformSystem.SetCoordinates(ent.Value, targetCoords);
            playerTransform.AttachToGridOrMap();
        }
        else
        {
            foreach (var victim in args)
            {
                if (victim == target)
                    continue;

                if (!TryGetTransformFromUidOrUsername(victim, shell, out var uid, out var victimTransform))
                    return;

                transformSystem.SetCoordinates(uid.Value, targetCoords);
                victimTransform.AttachToGridOrMap();
            }
        }
    }

    private bool TryGetTransformFromUidOrUsername(
        string str,
        IConsoleShell shell,
        [NotNullWhen(true)] out EntityUid? victimUid,
        [NotNullWhen(true)] out TransformComponent? transform)
    {
        if (EntityUid.TryParse(str, out var uid) && _entities.TryGetComponent(uid, out transform))
        {
            victimUid = uid;
            return true;
        }

        if (_players.Sessions.TryFirstOrDefault(x => x.ConnectedClient.UserName == str, out var session)
            && _entities.TryGetComponent(session.AttachedEntity, out transform))
        {
            victimUid = session.AttachedEntity;
            return true;
        }

        shell.WriteError(Loc.GetString("cmd-tpto-parse-error", ("str",str)));

        transform = null;
        victimUid = default;
        return false;
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 0)
            return CompletionResult.Empty;

        var last = args[^1];

        var users = _players.Sessions
            .Select(x => x.Name ?? string.Empty)
            .Where(x => !string.IsNullOrWhiteSpace(x) && x.StartsWith(last, StringComparison.CurrentCultureIgnoreCase));

        var hint = args.Length == 1 ? "cmd-tpto-destination-hint" : "cmd-tpto-victim-hint";
        hint = Loc.GetString(hint);

        var opts = CompletionResult.FromHintOptions(users, hint);
        if (last != string.Empty && !EntityUid.TryParse(last, out _))
            return opts;

        return CompletionResult.FromHintOptions(opts.Options.Concat(CompletionHelper.EntityUids(last, _entities)), hint);
    }
}

sealed partial class LocationCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _ent = default!;

    public override string Command => "loc";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { AttachedEntity: { } entity })
            return;

        var pt = _ent.GetComponent<TransformComponent>(entity);
        var pos = pt.Coordinates;

        shell.WriteLine($"MapID:{pos.GetMapId(_ent)} GridUid:{pos.GetGridUid(_ent)} X:{pos.X:N2} Y:{pos.Y:N2}");
    }
}

sealed partial class TpGridCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _ent = default!;
    [Dependency] private IMapManager _map = default!;

    public override string Command => "tpgrid";
    public override bool RequireServerOrSingleplayer => true;

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 3 or > 4)
        {
            shell.WriteError($"Usage: {Help}");
            return;
        }

        var gridId = EntityUid.Parse(args[0]);
        var xPos = float.Parse(args[1], CultureInfo.InvariantCulture);
        var yPos = float.Parse(args[2], CultureInfo.InvariantCulture);

        if (!_ent.EntityExists(gridId))
        {
            shell.WriteError($"Entity does not exist: {args[0]}");
            return;
        }

        if (!_ent.HasComponent<MapGridComponent>(gridId))
        {
            shell.WriteError($"No grid found with id {args[0]}");
            return;
        }

        var gridXform = _ent.GetComponent<TransformComponent>(gridId);
        var mapId = args.Length == 4 ? new MapId(int.Parse(args[3])) : gridXform.MapID;

        gridXform.Coordinates = new EntityCoordinates(_map.GetMapEntityId(mapId), new Vector2(xPos, yPos));

        shell.WriteLine("Grid was teleported.");
    }
}

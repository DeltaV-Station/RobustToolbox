﻿using System.Globalization;
using System.Linq;
using System.Text;
using Robust.Server.Interfaces.Console;
using Robust.Server.Interfaces.Maps;
using Robust.Server.Interfaces.Player;
using Robust.Server.Interfaces.Timing;
using Robust.Shared.Interfaces.Map;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Robust.Server.Console.Commands
{
    class AddMapCommand : IClientCommand
    {
        public string Command => "addmap";
        public string Description => "Adds a new empty map to the round. If the mapID already exists, this command does nothing.";
        public string Help => "addmap <mapID> [initialize]";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (args.Length < 1)
                return;

            var mapId = new MapId(int.Parse(args[0]));

            var mapMgr = IoCManager.Resolve<IMapManager>();
            var pauseMgr = IoCManager.Resolve<IPauseManager>();

            if (!mapMgr.MapExists(mapId))
            {
                mapMgr.CreateMap(mapId);
                if (args.Length >= 2 && args[1] == "false")
                {
                    pauseMgr.AddUninitializedMap(mapId);
                }
                shell.SendText(player, $"Map with ID {mapId} created.");
                return;
            }

            shell.SendText(player, $"Map with ID {mapId} already exists!");
        }
    }

    class RemoveMapCommand : IClientCommand
    {
        public string Command => "rmmap";
        public string Description => "Removes a map from the world. You cannot remove nullspace.";
        public string Help => "rmmap <mapId>";
        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (args.Length != 1)
            {
                shell.SendText(player, "Wrong number of args.");
                return;
            }

            var mapId = new MapId(int.Parse(args[0]));
            var mapManager = IoCManager.Resolve<IMapManager>();

            if (!mapManager.MapExists(mapId))
            {
                shell.SendText(player, $"Map {mapId.Value} does not exist.");
                return;
            }

            mapManager.DeleteMap(mapId);
            shell.SendText(player, $"Map {mapId.Value} was removed.");
        }
    }

    public class SaveBp : IClientCommand
    {
        public string Command => "savebp";
        public string Description => "Serializes a grid to disk.";
        public string Help => "savebp <gridID> <Path>";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (args.Length < 2)
            {
                shell.SendText(player, "Not enough arguments.");
                return;
            }

            if (!int.TryParse(args[0], out var intGridId))
            {
                shell.SendText(player, "Not a valid grid ID.");
                return;
            }

            var gridId = new GridId(intGridId);

            var mapManager = IoCManager.Resolve<IMapManager>();

            // no saving default grid
            if (!mapManager.TryGetGrid(gridId, out var grid))
            {
                shell.SendText(player, "That grid does not exist.");
                return;
            }

            if (grid.IsDefaultGrid)
            {
                shell.SendText(player, "Cannot save a default grid.");
                return;
            }

            IoCManager.Resolve<IMapLoader>().SaveBlueprint(gridId, args[1]);
            shell.SendText(player, "Save successful. Look in the user data directory.");
        }
    }

    public class LoadBp : IClientCommand
    {
        public string Command => "loadbp";
        public string Description => "Loads a blueprint from disk into the game.";
        public string Help => "loadbp <MapID> <Path>";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (args.Length < 2)
            {
                return;
            }

            if (!int.TryParse(args[0], out var intMapId))
            {
                return;
            }

            var mapId = new MapId(intMapId);

            // no loading into null space
            if (mapId == MapId.Nullspace)
            {
                shell.SendText(player, "Cannot load into nullspace.");
                return;
            }

            var mapManager = IoCManager.Resolve<IMapManager>();
            if (!mapManager.MapExists(mapId))
            {
                shell.SendText(player, "Target map does not exist.");
                return;
            }

            var mapLoader = IoCManager.Resolve<IMapLoader>();
            mapLoader.LoadBlueprint(mapId, args[1]);
        }
    }

    public class SaveMap : IClientCommand
    {
        public string Command => "savemap";
        public string Description => "Serializes a map to disk.";
        public string Help => "savemap <MapID> <Path>";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (args.Length < 1)
                return;

            if (!int.TryParse(args[0], out var intMapId))
                return;

            var mapID = new MapId(intMapId);

            // no saving null space
            if (mapID == MapId.Nullspace)
                return;

            var mapManager = IoCManager.Resolve<IMapManager>();
            if (!mapManager.MapExists(mapID))
                return;

            // TODO: Parse path
            IoCManager.Resolve<IMapLoader>().SaveMap(mapID, "Maps/Demo/DemoMap.yaml");
        }
    }

    public class LoadMap : IClientCommand
    {
        public string Command => "loadmap";
        public string Description => "Loads a map from disk into the game.";
        public string Help => "loadmap <MapID> <Path>";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (args.Length < 1)
                return;

            if (!int.TryParse(args[0], out var intMapId))
                return;

            var mapID = new MapId(intMapId);

            // no loading null space
            if (mapID == MapId.Nullspace)
            {
                shell.SendText(player, "You cannot load into map 0.");
                return;
            }

            var mapManager = IoCManager.Resolve<IMapManager>();
            if (mapManager.MapExists(mapID))
            {
                shell.SendText(player, $"Map {mapID} already exists.");
                return;
            }

            // TODO: Parse path
            var mapPath = "Maps/Demo/DemoMap.yaml";
            IoCManager.Resolve<IMapLoader>().LoadMap(mapID, mapPath);
            shell.SendText(player, $"Map {mapID} has been loaded from {mapPath}.");
        }
    }

    class LocationCommand : IClientCommand
    {
        public string Command => "loc";
        public string Description => "Prints the absolute location of the player's entity to console.";
        public string Help => "loc";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if(player.AttachedEntity == null)
                return;

            var pos = player.AttachedEntity.Transform.GridPosition;

            shell.SendText(player, $"MapID:{IoCManager.Resolve<IMapManager>().GetGrid(pos.GridID).ParentMapId} GridID:{pos.GridID} X:{pos.X:N2} Y:{pos.Y:N2}");
        }
    }

    class PauseMapCommand : IClientCommand
    {
        public string Command => "pausemap";
        public string Description => "Pauses a map, pausing all simulation processing on it.";
        public string Help => "Usage: pausemap <map ID>";
        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            var arg = args[0];
            var mapId = new MapId(int.Parse(arg, CultureInfo.InvariantCulture));

            var pauseManager = IoCManager.Resolve<IPauseManager>();
            var mapManager = IoCManager.Resolve<IMapManager>();
            if (!mapManager.MapExists(mapId))
            {
                shell.SendText(player, "That map does not exist.");
                return;
            }
            pauseManager.SetMapPaused(mapId, true);
        }
    }

    class UnpauseMapCommand : IClientCommand
    {
        public string Command => "unpausemap";
        public string Description => "unpauses a map, resuming all simulation processing on it.";
        public string Help => "Usage: unpausemap <map ID>";
        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            var arg = args[0];
            var mapId = new MapId(int.Parse(arg, CultureInfo.InvariantCulture));

            var pauseManager = IoCManager.Resolve<IPauseManager>();
            var mapManager = IoCManager.Resolve<IMapManager>();
            if (!mapManager.MapExists(mapId))
            {
                shell.SendText(player, "That map does not exist.");
                return;
            }
            pauseManager.SetMapPaused(mapId, false);
        }
    }

    class QueryMapPausedCommand : IClientCommand
    {
        public string Command => "querymappaused";
        public string Description => "Check whether a map is paused or not.";
        public string Help => "Usage: querymappaused <map ID>";
        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            var arg = args[0];
            var mapId = new MapId(int.Parse(arg, CultureInfo.InvariantCulture));

            var pauseManager = IoCManager.Resolve<IPauseManager>();
            var mapManager = IoCManager.Resolve<IMapManager>();
            if (!mapManager.MapExists(mapId))
            {
                shell.SendText(player, "That map does not exist.");
                return;
            }
            shell.SendText(player, pauseManager.IsMapPaused(mapId).ToString());
        }
    }

    class TpGridCommand : IClientCommand
    {
        public string Command => "tpgrid";
        public string Description => "Teleports a grid to a new location.";
        public string Help => "tpgrid <gridId> <X> <Y> [<MapId>]";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (args.Length < 3 || args.Length > 4)
            {
                shell.SendText(player, "Wrong number of args.");
            }

            var gridId = new GridId(int.Parse(args[0]));
            var xpos = float.Parse(args[1]);
            var ypos = float.Parse(args[2]);

            var mapManager = IoCManager.Resolve<IMapManager>();

            if (mapManager.TryGetGrid(gridId, out var grid))
            {
                var mapId = args.Length == 4 ? new MapId(int.Parse(args[3])) : grid.ParentMapId;

                grid.ParentMapId = mapId;
                grid.WorldPosition = new Vector2(xpos, ypos);

                shell.SendText(player, "Grid was teleported.");
            }
        }
    }

    class RemoveGridCommand : IClientCommand
    {
        public string Command => "rmgrid";
        public string Description => "Removes a grid from a map. You cannot remove the default grid.";
        public string Help => "rmgrid <gridId>";
        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (args.Length != 1)
            {
                shell.SendText(player, "Wrong number of args.");
                return;
            }

            var gridId = new GridId(int.Parse(args[0]));
            var mapManager = IoCManager.Resolve<IMapManager>();

            if (!mapManager.GridExists(gridId))
            {
                shell.SendText(player, $"Grid {gridId.Value} does not exist.");
                return;
            }

            mapManager.DeleteGrid(gridId);
            shell.SendText(player, $"Grid {gridId.Value} was removed.");
        }
    }

    internal sealed class RunMapInitCommand : IClientCommand
    {
        public string Command => "mapinit";
        public string Description => default;
        public string Help => "mapinit <mapID>";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            if (args.Length != 1)
            {
                shell.SendText(player, "Wrong number of args.");
                return;
            }

            var mapManager = IoCManager.Resolve<IMapManager>();
            var pauseManager = IoCManager.Resolve<IPauseManager>();

            var arg = args[0];
            var mapId = new MapId(int.Parse(arg, CultureInfo.InvariantCulture));

            if (!mapManager.MapExists(mapId))
            {
                shell.SendText(player, "Map does not exist!");
                return;
            }

            if (pauseManager.IsMapInitialized(mapId))
            {
                shell.SendText(player, "Map is already initialized!");
                return;
            }

            pauseManager.DoMapInitialize(mapId);
        }
    }

    internal sealed class ListMapsCommand : IClientCommand
    {
        public string Command => "lsmap";
        public string Description => default;
        public string Help => "lsmap";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            var mapManager = IoCManager.Resolve<IMapManager>();
            var pauseManager = IoCManager.Resolve<IPauseManager>();

            var msg = new StringBuilder();

            foreach (var mapId in mapManager.GetAllMapIds().OrderBy(id => id.Value))
            {
                msg.AppendFormat("{0}: default grid: {1}, init: {2}, paused: {3}, ent: {5}, grids: {4}\n",
                    mapId, mapManager.GetDefaultGridId(mapId), pauseManager.IsMapInitialized(mapId),
                    pauseManager.IsMapPaused(mapId),
                    string.Join(",", mapManager.GetAllMapGrids(mapId).Select(grid => grid.Index)),
                    mapManager.GetMapEntityId(mapId));
            }

            shell.SendText(player, msg.ToString());
        }
    }

    internal sealed class ListGridsCommand : IClientCommand
    {
        public string Command => "lsgrid";
        public string Description => default;
        public string Help => "lsgrid";

        public void Execute(IConsoleShell shell, IPlayerSession player, string[] args)
        {
            var mapManager = IoCManager.Resolve<IMapManager>();

            var msg = new StringBuilder();

            foreach (var grid in mapManager.GetAllGrids().OrderBy(grid => grid.Index.Value))
            {
                msg.AppendFormat("{0}: map: {1}, ent: {4}, default: {2}, pos: {3} \n",
                    grid.Index, grid.ParentMapId, grid.IsDefaultGrid, grid.WorldPosition, grid.GridEntityId);
            }

            shell.SendText(player, msg.ToString());
        }
    }
}

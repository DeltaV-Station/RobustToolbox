﻿using SS14.Server.Interfaces.Player;
using SS14.Server.Interfaces.ServerConsole;
using SS14.Shared.IoC;
using System;

namespace SS14.Server.ServerConsole.Commands
{
    public class ListPlayers : IConsoleCommand
    {
        public string Command => "listplayers";
        public string Description => "Lists all players currently connected";
        public string Help => "Usage: listplayers";

        public void Execute(params string[] args)
        {
            IPlayerSession[] players = IoCManager.Resolve<IPlayerManager>().GetAllPlayers();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Current Players:\n");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("{0,20}{1,16}{2,12}{3, 14}{4,9}", "Player Name", "IP Address", "Status", "Playing Time",
                              "Ping");
            foreach (IPlayerSession p in players)
            {
                Console.Write("{0,20}", p.name);
                Console.WriteLine("{0,16}{1,12}{2,14}{3,9}",
                                  p.connectedClient.RemoteEndPoint.Address,
                                  p.status.ToString(),
                                  (DateTime.Now - p.ConnectedTime).ToString(@"hh\:mm\:ss"),
                                  Math.Round(p.connectedClient.AverageRoundtripTime*1000, 2) + "ms");
            }
        }
    }
}

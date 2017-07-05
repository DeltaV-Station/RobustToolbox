﻿using Lidgren.Network;
using SS14.Server.Interfaces;
using SS14.Server.Interfaces.Chat;
using SS14.Server.Interfaces.ClientConsoleHost;
using SS14.Server.Interfaces.GameObjects;
using SS14.Server.Interfaces.GameState;
using SS14.Server.Interfaces.Log;
using SS14.Server.Interfaces.Map;
using SS14.Server.Interfaces.Network;
using SS14.Server.Interfaces.Placement;
using SS14.Server.Interfaces.Player;
using SS14.Server.Interfaces.Round;
using SS14.Server.Interfaces.Serialization;
using SS14.Server.Interfaces.ServerConsole;
using SS14.Server.Modules;
using SS14.Server.Modules.Client;
using SS14.Server.Map;
using SS14.Server.Round;
using SS14.Shared;
using SS14.Shared.Configuration;
using SS14.Shared.GameObjects;
using SS14.Shared.GameStates;
using SS14.Shared.IoC;
using SS14.Shared.Interfaces.Configuration;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.Log;
using SS14.Shared.ServerEnums;
using SS14.Shared.Utility;
using SS14.Shared.Prototypes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using MainLoopTimer = SS14.Server.Timing.MainLoopTimer;

namespace SS14.Server
{
    public class SS14Server : ISS14Server
    {
        private const int GameCountdown = 15;
        private readonly List<float> frameTimes = new List<float>();

        public Dictionary<NetConnection, IClient> ClientList = new Dictionary<NetConnection, IClient>();
        private readonly Dictionary<NetConnection, DateTime> _clientListLastSeen = new Dictionary<NetConnection, DateTime>();

        public DateTime Time;
        private bool _active;
        private int _lastAnnounced;

        // State update vars
        private float serverClock;

        private uint _lastState;
        private DateTime _lastStateTime = DateTime.Now;
        private uint _oldestAckedState;
        private DateTime _startAt;
        private Object mainLoopTimer; //TODO: make a pretty interface for this
        private static readonly AutoResetEvent are = new AutoResetEvent(true);
        public Stopwatch stopWatch = new Stopwatch();
        private uint basePeriod;
        private uint period;
        private int updateRate = 20; //20 updates per second
        public string ConsoleTitle { get; private set; }

        public bool Active => _active;

        #region Server Settings

        public DateTime LastUpdate;
        private GameType _gameType = GameType.Game;
        private string _serverMapName = "SavedMap";
        private int _serverMaxPlayers = 32;
        private string _serverName = "SS13 Server";
        private int _serverPort = 1212;
        private string _serverWelcomeMessage = "Welcome to the server!";
        private DateTime lastBytesUpdate = DateTime.Now;
        private int lastRecievedBytes;
        private int lastSentBytes;

        public bool Start()
        {
            var configMgr = IoCManager.Resolve<IConfigurationManager>();
            configMgr.LoadFromFile(PathHelpers.ExecutableRelativeFile("server_config.toml"));

            configMgr.RegisterCVar("log.path", "logs", CVarFlags.ARCHIVE);
            configMgr.RegisterCVar("log.format", "log_%(date)s-%(time)s.txt", CVarFlags.ARCHIVE);
            configMgr.RegisterCVar("log.level", LogLevel.Information, CVarFlags.ARCHIVE);
            configMgr.RegisterCVar("log.enabled", true, CVarFlags.ARCHIVE);

            configMgr.RegisterCVar("net.tickrate", 66, CVarFlags.ARCHIVE | CVarFlags.REPLICATED | CVarFlags.SERVER);

            string logPath = configMgr.GetCVar<string>("log.path");
            string logFormat = configMgr.GetCVar<string>("log.format");
            string logFilename = logFormat.Replace("%(date)s", DateTime.Now.ToString("yyyyMMdd")).Replace("%(time)s", DateTime.Now.ToString("hhmmss"));
            string fullPath = Path.Combine(logPath, logFilename);

            if (!Path.IsPathRooted(fullPath))
            {
                logPath = PathHelpers.ExecutableRelativeFile(fullPath);
            }

            // Create log directory if it does not exist yet.
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));

            LogManager.CurrentLevel = configMgr.GetCVar<LogLevel>("log.level");
            LogManager.LogPath = logPath;

            TickRate = configMgr.GetCVar<int>("net.tickrate");
            ServerRate = 1000.0f / TickRate;

            Time = DateTime.Now;

            LoadSettings();

            var prototypeManager = IoCManager.Resolve<IPrototypeManager>();
            prototypeManager.LoadDirectory(PathHelpers.ExecutableRelativeFile("Prototypes"));
            prototypeManager.Resync();
            IoCManager.Resolve<ISS14NetServer>().Start();
            IoCManager.Resolve<IChatManager>().Initialize();
            IoCManager.Resolve<IPlayerManager>().Initialize(this);
            IoCManager.Resolve<IPlacementManager>().Initialize(this);
            IoCManager.Resolve<IMapManager>().Initialize();

            StartLobby();
            StartGame();

            _active = true;
            return false;
        }

        public float ServerRate // desired server frame (tick) time in milliseconds
        { get; private set; }

        public float TickRate // desired server frames (ticks) per second
        { get; private set; }

        #endregion Server Settings

        #region ISS13Server Members

        [Dependency]
        private readonly IServerEntityManager EntityManager;
        [Dependency]
        private readonly IComponentManager ComponentManager;
        [Dependency]
        private readonly IServerLogManager LogManager;
        public RunLevel Runlevel { get; private set; } = RunLevel.Init;

        public void Restart()
        {
            Logger.Log("Restarting Server...");
            IoCManager.Resolve<IPlayerManager>().SendJoinLobbyToAll();
            SendGameStateUpdate(true, true);
            DisposeForRestart();
            StartLobby();
        }

        public void Shutdown(string reason = null)
        {
            if (reason == null)
                Logger.Log("Shutting down...");
            else
                Logger.Log(string.Format("{0}, shutting down...", reason));
            _active = false;
        }

        public IClient GetClient(NetConnection clientConnection)
        {
            return ClientList[clientConnection];
        }

        public void SaveMap()
        {
            IoCManager.Resolve<IMapManager>().SaveMap(_serverMapName);
        }

        public void SaveEntities()
        {
            EntityManager.SaveEntities();
        }

        public IMapManager GetMap()
        {
            return IoCManager.Resolve<IMapManager>();
        }

        public void SetServerInstance(ISS14Server server)
        {
        }

        #endregion ISS13Server Members

        #region server mainloop

        // The main server loop
        public void MainLoop()
        {
            basePeriod = 1;
            period = basePeriod;

            var timerObject = new MainLoopTimer();
            stopWatch.Start();
            mainLoopTimer = timerObject.mainLoopTimer.CreateMainLoopTimer(() =>
                                                       {
                                                           RunLoop();
                                                       }, period);

            while (Active)
            {
                are.WaitOne(-1);

                DoMainLoopStuff();
            }

            Cleanup();

            /*   TimerCallback tcb = RunLoop;
            var due = 1;// (long)ServerRate / 3;
            stopWatch.Start(); //Start the clock
            mainLoopTimer = new Timer(tcb, are, 0, due);
            are.WaitOne(-1);*/
        }

        public void RunLoop()
        {
            are.Set();
        }

        private void DoMainLoopStuff()
        {
            float elapsedTime;
            elapsedTime = (stopWatch.ElapsedTicks / (float)Stopwatch.Frequency);
            float elapsedMilliseconds = elapsedTime * 1000;

            if (elapsedMilliseconds < ServerRate && ServerRate - elapsedMilliseconds >= 0.5f)
            {
                return;
            }
            stopWatch.Restart(); //Reset the stopwatch so we get elapsed time next time

            //Elapsed time in seconds since the last tick
            serverClock += elapsedTime;

            //Begin update time
            Time = DateTime.Now;
            if (frameTimes.Count >= TickRate)
                frameTimes.RemoveAt(0);
            float rate = 1 / elapsedTime;
            frameTimes.Add(rate);

            if ((DateTime.Now - lastBytesUpdate).TotalMilliseconds > 1000)
            {
                string netstats = UpdateBPS();
                Console.Title = string.Format("FPS: {0:N2} | Net: ({1}) | Memory: {2:N0} KiB",
                    Math.Round(frameTimeAverage(), 2),
                    netstats,
                    System.Diagnostics.Process.GetCurrentProcess().PrivateMemorySize64 >> 10);
                lastBytesUpdate = DateTime.Now;
            }

            ProcessPackets();

            //Update takes elapsed time in seconds.
            Update(elapsedTime);

            IoCManager.Resolve<IConsoleManager>().Update();
        }

        private void Cleanup()
        {
            Console.Title = "";
        }

        private string UpdateBPS()
        {
            string BPS = string.Format("Send: {0:N0} KiB/s, Recv: {1:N0} KiB/s",
                (IoCManager.Resolve<ISS14NetServer>().Statistics.SentBytes - lastSentBytes) >> 10,
                (IoCManager.Resolve<ISS14NetServer>().Statistics.ReceivedBytes - lastRecievedBytes) >> 10);
            lastSentBytes = IoCManager.Resolve<ISS14NetServer>().Statistics.SentBytes;
            lastRecievedBytes = IoCManager.Resolve<ISS14NetServer>().Statistics.ReceivedBytes;
            return BPS;
        }

        private float frameTimeAverage()
        {
            if (frameTimes.Count == 0)
                return 0;
            return frameTimes.Average(p => p);
        }

        public void ProcessPackets()
        {
            NetIncomingMessage msg;
            while ((msg = IoCManager.Resolve<ISS14NetServer>().ReadMessage()) != null)
            {
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.VerboseDebugMessage:
                        Logger.Log(msg.ReadString(), LogLevel.Debug);
                        break;

                    case NetIncomingMessageType.DebugMessage:
                        Logger.Log(msg.ReadString(), LogLevel.Debug);
                        break;

                    case NetIncomingMessageType.WarningMessage:
                        Logger.Log(msg.ReadString(), LogLevel.Warning);
                        break;

                    case NetIncomingMessageType.ErrorMessage:
                        Logger.Log(msg.ReadString(), LogLevel.Error);
                        break;

                    case NetIncomingMessageType.Data:
                        if (ClientList.ContainsKey(msg.SenderConnection))
                        {
                            HandleData(msg);
                        }
                        break;

                    case NetIncomingMessageType.StatusChanged:
                        HandleStatusChanged(msg);
                        break;

                    default:
                        Logger.Log("Unhandled type: " + msg.MessageType, LogLevel.Error);
                        break;
                }
                UpdateLastSeen(msg);
                IoCManager.Resolve<ISS14NetServer>().Recycle(msg);
            }
        }

        private void UpdateLastSeen(NetIncomingMessage msg)
        {
            DateTime currentTime = DateTime.Now;
            NetConnection sender = msg.SenderConnection;
            if (sender == null || sender.Status == NetConnectionStatus.Disconnected || sender.Status == NetConnectionStatus.Disconnecting)
            {
                return;
            }
            if (_clientListLastSeen.ContainsKey(sender))
            {
                _clientListLastSeen[sender] = currentTime;
            }
            else
            {
                _clientListLastSeen.Add(sender, currentTime);
            }
            List<NetConnection> cleanupConnections = new List<NetConnection>();
            foreach (KeyValuePair<NetConnection, DateTime> client in _clientListLastSeen)
            {
                if (currentTime.Subtract(client.Value).TotalSeconds >= 5)
                {
                    Logger.Log(String.Format("Client Timeout: Kicking client {0}", client.Key.RemoteEndPoint));
                    client.Key.Disconnect("No message was recieved in 60 seconds, you have been kicked from the server.");
                    cleanupConnections.Add(sender);
                }
            }
            foreach (NetConnection conn in cleanupConnections)
            {
                CleanupClientConnection(conn);
            }
        }

        public void Update(float frameTime)
        {
            switch (Runlevel)
            {
                case RunLevel.Game:

                    ComponentManager.Update(frameTime);
                    EntityManager.Update(frameTime);
                    var start = stopWatch.ElapsedTicks;
                    //((AtmosManager)IoCManager.Resolve<IAtmosManager>()).Update(frameTime);
                    var end = stopWatch.ElapsedTicks;
                    var atmosTime = (end - start) / (float)Stopwatch.Frequency * 1000;
                    IoCManager.Resolve<IRoundManager>().CurrentGameMode.Update();
                    GC.KeepAlive(atmosTime);

                    break;

                case RunLevel.Lobby:

                    TimeSpan countdown = _startAt.Subtract(DateTime.Now);
                    if (_lastAnnounced != countdown.Seconds)
                    {
                        _lastAnnounced = countdown.Seconds;
                        IoCManager.Resolve<IChatManager>().SendChatMessage(ChatChannel.Server,
                                                                           "Starting in " + _lastAnnounced + " seconds...",
                                                                           "", 0);
                    }
                    if (countdown.Seconds <= 0)
                    {
                        StartGame();
                    }

                    break;

                default:
                    // Unknown game state
                    break;
            }
            LastUpdate = Time;
            SendGameStateUpdate();
        }

        public void UpdateAtmos(float frameTime)
        {
            /*
            var t = new Thread(() => RealUpdateAtmos(frameTime));
            t.Start();
            return t;*/
        }

        public void RealUpdateAtmos(float frameTime)
        {
        }

        public void SendGameStateUpdate(bool force = false, bool forceFullState = false)
        {
            //Obey the updates per second limit
            TimeSpan elapsed = Time - _lastStateTime;
            if (force || elapsed.TotalMilliseconds > (1000 / updateRate))
            {
                //Save last state time
                _lastStateTime = Time;
                //Create a new GameState object
                var stateManager = IoCManager.Resolve<IGameStateManager>();
                var state = new GameState(++_lastState);
                if (EntityManager != null)
                    state.EntityStates = EntityManager.GetEntityStates();
                state.PlayerStates = IoCManager.Resolve<IPlayerManager>().GetPlayerStates();
                stateManager.Add(state.Sequence, state);

                //LogManager.Log("Update " + _lastState + " sent.");
                List<NetConnection> connections = IoCManager.Resolve<ISS14NetServer>().Connections;
                if (connections.Count == 0)
                {
                    //No clients -- don't send state
                    _oldestAckedState = _lastState;
                    stateManager.Clear();
                }
                else
                {
                    foreach (
                        NetConnection c in
                            IoCManager.Resolve<ISS14NetServer>().Connections.Where(
                                c => c.Status == NetConnectionStatus.Connected))
                    {
                        IPlayerSession session = IoCManager.Resolve<IPlayerManager>().GetSessionByConnection(c);
                        if (session == null || (session.status != SessionStatus.InGame && session.status != SessionStatus.InLobby))
                            continue;
                        NetOutgoingMessage stateMessage = IoCManager.Resolve<ISS14NetServer>().CreateMessage();
                        uint lastStateAcked = stateManager.GetLastStateAcked(c);

                        if (lastStateAcked == 0)// || forceFullState)
                        {
                            state.WriteStateMessage(stateMessage);
                            //LogManager.Log("Full state of size " + length + " sent to " + c.RemoteUniqueIdentifier);
                        }
                        else
                        {
                            stateMessage.Write((byte)NetMessage.StateUpdate);
                            GameStateDelta delta = stateManager.GetDelta(c, _lastState);
                            delta.WriteDelta(stateMessage);
                            //LogManager.Log("Delta of size " + delta.Size + " sent to " + c.RemoteUniqueIdentifier);
                        }

                        IoCManager.Resolve<ISS14NetServer>().SendMessage(stateMessage, c, NetDeliveryMethod.Unreliable);
                    }
                }
                stateManager.Cull();
            }
        }

        #endregion server mainloop

        public void LoadSettings()
        {
            var cfgMgr = IoCManager.Resolve<IConfigurationManager>();

            cfgMgr.RegisterCVar("game.hostname", "MyServer", CVarFlags.ARCHIVE);
            cfgMgr.RegisterCVar("game.mapname", "SavedMap", CVarFlags.ARCHIVE);
            cfgMgr.RegisterCVar("game.maxplayers", 32, CVarFlags.ARCHIVE);
            cfgMgr.RegisterCVar("game.type", GameType.Game);
            cfgMgr.RegisterCVar("game.welcomemsg", "Welcome to the server!", CVarFlags.ARCHIVE);

            _serverPort = cfgMgr.GetCVar<int>("net.port");
            _serverName = cfgMgr.GetCVar<string>("game.hostname");
            _serverMapName = cfgMgr.GetCVar<string>("game.mapname");
            _serverMaxPlayers = cfgMgr.GetCVar<int>("game.maxplayers");
            _gameType = cfgMgr.GetCVar<GameType>("game.type");
            _serverWelcomeMessage = cfgMgr.GetCVar<string>("game.welcomemsg");

            Logger.Log("Port: " + _serverPort);
            Logger.Log("Name: " + _serverName);
            Logger.Log("TickRate: " + TickRate + "(" + ServerRate + "ms)");
            Logger.Log("Map: " + _serverMapName);
            Logger.Log("Max players: " + _serverMaxPlayers);
            Logger.Log("Game type: " + _gameType);
            Logger.Log("Welcome message: " + _serverWelcomeMessage);
        }

        /// <summary>
        /// Controls what modules are running.
        /// </summary>
        /// <param name="runlevel"></param>
        public void InitModules(RunLevel runlevel = RunLevel.Lobby)
        {
            if (runlevel == Runlevel)
                return;

            Runlevel = runlevel;
            if (Runlevel == RunLevel.Lobby)
            {
                _startAt = DateTime.Now.AddSeconds(GameCountdown);
            }
            else if (Runlevel == RunLevel.Game)
            {
                IoCManager.Resolve<IMapManager>().LoadMap(_serverMapName);
                EntityManager.Initialize();
                IoCManager.Resolve<IRoundManager>().CurrentGameMode.StartGame();
            }
        }

        public void StartLobby()
        {
            IoCManager.Resolve<IRoundManager>().Initialize(new Gamemode(this));
            InitModules(RunLevel.Lobby);
        }

        public void StartGame()
        {
            InitModules(RunLevel.Game);
            IoCManager.Resolve<IPlayerManager>().SendJoinGameToAll();
        }

        public void DisposeForRestart()
        {
            IoCManager.Resolve<IPlayerManager>().DetachAll();
            EntityManager.Shutdown();
            GC.Collect();
        }

        public void HandleConnectionApproval(NetConnection sender)
        {
            ClientList.Add(sender, new Client(sender));
        }

        public void SendWelcomeInfo(NetConnection connection)
        {
            NetOutgoingMessage welcomeMessage = IoCManager.Resolve<ISS14NetServer>().CreateMessage();
            welcomeMessage.Write((byte)NetMessage.WelcomeMessage);
            welcomeMessage.Write(_serverName);
            welcomeMessage.Write(_serverPort);
            welcomeMessage.Write(_serverWelcomeMessage);
            welcomeMessage.Write(_serverMaxPlayers);
            welcomeMessage.Write(_serverMapName);
            welcomeMessage.Write(IoCManager.Resolve<IRoundManager>().CurrentGameMode.Name);
            IoCManager.Resolve<ISS14NetServer>().SendMessage(welcomeMessage, connection,
                                                             NetDeliveryMethod.ReliableOrdered);
            SendNewPlayerCount();
        }

        public void SendNewPlayerCount()
        {
            NetOutgoingMessage playercountMessage = IoCManager.Resolve<ISS14NetServer>().CreateMessage();
            playercountMessage.Write((byte)NetMessage.PlayerCount);
            playercountMessage.Write((byte)ClientList.Count);
            IoCManager.Resolve<ISS14NetServer>().SendToAll(playercountMessage);
        }

        public void SendPlayerList(NetConnection connection)
        {
            NetOutgoingMessage playerListMessage = IoCManager.Resolve<ISS14NetServer>().CreateMessage();
            playerListMessage.Write((byte)NetMessage.PlayerList);
            playerListMessage.Write((byte)ClientList.Count);

            foreach (NetConnection conn in ClientList.Keys)
            {
                IPlayerSession plrSession = IoCManager.Resolve<IPlayerManager>().GetSessionByConnection(conn);
                playerListMessage.Write(plrSession.name);
                playerListMessage.Write((byte)plrSession.status);
                playerListMessage.Write(ClientList[conn].NetConnection.AverageRoundtripTime);
            }
            IoCManager.Resolve<ISS14NetServer>().SendMessage(playerListMessage, connection,
                                                             NetDeliveryMethod.ReliableOrdered);
        }

        public void HandleStatusChanged(NetIncomingMessage msg)
        {
            NetConnection sender = msg.SenderConnection;
            string senderIp = sender.RemoteEndPoint.Address.ToString();
            Logger.Log(String.Format("{0}: Status changed to {1}", senderIp, sender.Status.ToString()));

            switch (sender.Status)
            {
                case NetConnectionStatus.Connected:
                    Logger.Log(senderIp + ": Connection request");
                    if (ClientList.ContainsKey(sender)) // TODO Move this to a config to allow or disallowed shared IPAddress
                    {
                        Logger.Log(senderIp + ": Already connected", LogLevel.Error);
                        return;
                    }
                    HandleConnectionApproval(sender);
                    IoCManager.Resolve<IPlayerManager>().NewSession(sender);
                    // TODO move this to somewhere that makes more sense.

                    break;

                case NetConnectionStatus.Disconnected:
                    Logger.Log(senderIp + ": Disconnected");
                    IoCManager.Resolve<IPlayerManager>().EndSession(sender);
                    if (ClientList.ContainsKey(sender))
                    {
                        CleanupClientConnection(sender);
                    }
                    break;
            }
        }

        private void CleanupClientConnection(NetConnection sender)
        {
            _clientListLastSeen.Remove(sender);
            ClientList.Remove(sender);
        }

        /// <summary>
        /// Main method for routing incoming application network messages
        /// </summary>
        /// <param name="msg"></param>
        public void HandleData(NetIncomingMessage msg)
        {
            var messageType = (NetMessage)msg.ReadByte();
            switch (messageType)
            {
                case NetMessage.WelcomeMessage:
                    SendWelcomeInfo(msg.SenderConnection);
                    break;

                case NetMessage.ForceRestart:
                    Restart();
                    break;

                case NetMessage.RequestMap:
                    SendMap(msg.SenderConnection);
                    break;

                case NetMessage.PlayerList:
                    SendPlayerList(msg.SenderConnection);
                    break;

                case NetMessage.ClientName:
                    HandleClientName(msg);
                    break;

                case NetMessage.ChatMessage:
                    IoCManager.Resolve<IChatManager>().HandleNetMessage(msg);
                    break;

                case NetMessage.PlayerSessionMessage:
                    IoCManager.Resolve<IPlayerManager>().HandleNetworkMessage(msg);
                    break;

                case NetMessage.MapMessage:
                    IoCManager.Resolve<IMapManager>().HandleNetworkMessage(msg);
                    break;

                case NetMessage.PlacementManagerMessage:
                    IoCManager.Resolve<IPlacementManager>().HandleNetMessage(msg);
                    break;

                case NetMessage.EntityMessage:
                    EntityManager.HandleEntityNetworkMessage(msg);
                    break;

                case NetMessage.RequestEntityDeletion:
                    HandleAdminMessage(messageType, msg);
                    break;

                case NetMessage.StateAck:
                    HandleStateAck(msg);
                    break;

                case NetMessage.ConsoleCommand:
                    IoCManager.Resolve<IClientConsoleHost>().ProcessCommand(msg.ReadString(), msg.SenderConnection);
                    break;

                case NetMessage.ConsoleCommandRegister:
                    IoCManager.Resolve<IClientConsoleHost>().HandleRegistrationRequest(msg.SenderConnection);
                    break;
            }
        }

        public void HandleAdminMessage(NetMessage adminMsgType, NetIncomingMessage messageBody)
        {
            switch (adminMsgType)
            {
                case NetMessage.RequestEntityDeletion:
                    int entId = messageBody.ReadInt32();
                    //if (
                    //    IoCManager.Resolve<IPlayerManager>().GetSessionByConnection(messageBody.SenderConnection).
                    //        adminPermissions.isAdmin || true)
                    //TEMPORARY. REMOVE THE 'TRUE' LATER ON. !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                    //{
                    IEntity delEnt = EntityManager.GetEntity(entId);
                    if (delEnt != null) EntityManager.DeleteEntity(delEnt);
                    //}
                    break;
            }
        }

        public void HandleClientName(NetIncomingMessage msg)
        {
            string name = msg.ReadString();
            ClientList[msg.SenderConnection].PlayerName = name;
            string fixedname = name.Trim();
            if (fixedname.Length < 3)
                fixedname = "Player";
            IPlayerSession p = IoCManager.Resolve<IPlayerManager>().GetSessionByConnection(msg.SenderConnection);
            p.SetName(fixedname);
        }

        public void HandleStateAck(NetIncomingMessage msg)
        {
            uint sequence = msg.ReadUInt32();
            IoCManager.Resolve<IGameStateManager>().Ack(msg.SenderConnection.RemoteUniqueIdentifier, sequence);
            //LogManager.Log("State Acked: " + sequence + " by client " + msg.SenderConnection.RemoteUniqueIdentifier + ".");
        }

        // The size of the map being sent is almost exaclty 1 byte per tile.
        // The default 30x30 map is 900 bytes, a 100x100 one is 10,000 bytes (10kb).
        public void SendMap(NetConnection connection)
        {
            // Send Tiles
            IoCManager.Resolve<IMapManager>().SendMap(connection);

            // Lets also send them all the items and mobs.
            //EntityManager.SendEntities(connection);

            // Send atmos state to player
            //IoCManager.Resolve<IAtmosManager>().SendAtmosStateTo(connection);

            // Todo: Preempt this with the lobby.
            IoCManager.Resolve<IRoundManager>().SpawnPlayer(
                IoCManager.Resolve<IPlayerManager>().GetSessionByConnection(connection)); //SPAWN PLAYER
        }

        public void SendChangeTile(int x, int y, Tile newTile)
        {
            NetOutgoingMessage tileMessage = IoCManager.Resolve<ISS14NetServer>().CreateMessage();
            //tileMessage.Write((byte)NetMessage.ChangeTile);
            tileMessage.Write(x);
            tileMessage.Write(y);
            tileMessage.Write((uint)newTile);
            foreach (NetConnection connection in ClientList.Keys)
            {
                IoCManager.Resolve<ISS14NetServer>().SendMessage(tileMessage, connection,
                                                                 NetDeliveryMethod.ReliableOrdered);
                Logger.Log(connection.RemoteEndPoint.Address + ": Tile Change Being Sent", LogLevel.Debug);
            }
        }

        //Bogus -- this is some shit for the surrogate class in ServerServices.
    }
}

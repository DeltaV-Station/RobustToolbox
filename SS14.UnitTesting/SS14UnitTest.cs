﻿using NUnit.Framework;
using SFML.Graphics;
using SFML.System;
using OpenTK;
using OpenTK.Graphics;
using SS14.Client;
using SS14.Client.GameObjects;
using SS14.Client.Graphics;
using SS14.Client.Input;
using SS14.Client.Interfaces;
using SS14.Client.Interfaces.GameObjects;
using SS14.Client.Interfaces.Input;
using SS14.Client.Interfaces.Network;
using SS14.Client.Interfaces.Resource;
using SS14.Client.Interfaces.State;
using SS14.Client.Interfaces.UserInterface;
using SS14.Client.Interfaces.Utility;
using SS14.Client.Network;
using SS14.Client.State;
using SS14.Client.Reflection;
using SS14.Client.UserInterface;
using SS14.Client.Utility;
using SS14.Server;
using SS14.Server.Chat;
using SS14.Server.GameObjects;
using SS14.Server.GameStates;
using SS14.Server.Interfaces;
using SS14.Server.Interfaces.Chat;
using SS14.Server.Interfaces.ClientConsoleHost;
using SS14.Server.Interfaces.GameObjects;
using SS14.Server.Interfaces.GameState;
using SS14.Server.Interfaces.Log;
using SS14.Server.Interfaces.Placement;
using SS14.Server.Interfaces.Player;
using SS14.Server.Interfaces.Round;
using SS14.Server.Interfaces.ServerConsole;
using SS14.Server.Log;
using SS14.Server.Placement;
using SS14.Server.Player;
using SS14.Server.Reflection;
using SS14.Server.Round;
using SS14.Server.ServerConsole;
using SS14.Shared.Configuration;
using SS14.Shared.GameObjects;
using SS14.Shared.Interfaces.Configuration;
using SS14.Shared.Interfaces.GameObjects;
using SS14.Shared.Interfaces.Log;
using SS14.Shared.Interfaces.Map;
using SS14.Shared.Interfaces.Reflection;
using SS14.Shared.Interfaces.Serialization;
using SS14.Shared.IoC;
using SS14.Shared.Log;
using SS14.Shared.Map;
using SS14.Shared.Prototypes;
using SS14.Shared.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using SS14.Client.Graphics.Lighting;
using SS14.Client.Resources;
using SS14.Shared.ContentPack;
using SS14.Shared.Interfaces;
using SS14.Shared.Interfaces.Network;
using SS14.Shared.Interfaces.Physics;
using SS14.Shared.Interfaces.Timing;
using SS14.Shared.Network;
using SS14.Shared.Physics;
using SS14.Shared.Timing;
using SS14.Server.Interfaces.Maps;
using SS14.Server.Maps;

namespace SS14.UnitTesting
{
    public enum UnitTestProject
    {
        Server,
        Client
    }

    public abstract class SS14UnitTest
    {
        private FrameEventArgs frameEvent;
        public delegate void EventHandler();
        public static event EventHandler InjectedMethod;

        #region Options

        // TODO: make this figured out at runtime so we don't have to pass a compiler flag.
#if HEADLESS
        public const bool Headless = true;
#else
        public const bool Headless = false;
#endif

        // These properties are meant to be overriden to disable certain parts
        // Like loading resource packs, which isn't always needed.

        /// <summary>
        /// Whether the client resource pack should be loaded or not.
        /// </summary>
        public virtual bool NeedsResourcePack => false;

        /// <summary>
        /// Whether the client config should be loaded or not.
        /// </summary>
        public virtual bool NeedsClientConfig => false;

        public virtual UnitTestProject Project => UnitTestProject.Server;

        #endregion Options

        #region Accessors

        public IConfigurationManager GetConfigurationManager
        {
            get;
            private set;
        }

        public IResourceCache GetResourceCache
        {
            get;
            private set;
        }

        public Clock GetClock
        {
            get;
            set;
        }

        #endregion Accessors

        [OneTimeSetUp]
        public void BaseSetup()
        {
            TestFixtureAttribute a = Attribute.GetCustomAttribute(GetType(), typeof(TestFixtureAttribute)) as TestFixtureAttribute;
            if (NeedsResourcePack && Headless)
            {
                // Disable the test automatically.
                a.Explicit = true;
                return;
            }

            // Clear state across tests.
            IoCManager.Clear();
            RegisterIoC();

            var Assemblies = new List<Assembly>(4);
            string AssemblyDir = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
            switch (Project)
            {
                case UnitTestProject.Client:
                    Assemblies.Add(Assembly.LoadFrom(Path.Combine(AssemblyDir, "SS14.Client.exe")));
                    break;
                case UnitTestProject.Server:
                    Assemblies.Add(Assembly.LoadFrom(Path.Combine(AssemblyDir, "SS14.Server.exe")));
                    break;
                default:
                    throw new NotSupportedException($"Unknown testing project: {Project}");
            }

            Assemblies.Add(Assembly.LoadFrom(Path.Combine(AssemblyDir, "SS14.Shared.dll")));
            Assemblies.Add(Assembly.GetExecutingAssembly());

            IoCManager.Resolve<IReflectionManager>().LoadAssemblies(Assemblies);

            if (NeedsClientConfig)
            {
                //ConfigurationManager setup
                GetConfigurationManager = IoCManager.Resolve<IConfigurationManager>();
                GetConfigurationManager.LoadFromFile(PathHelpers.ExecutableRelativeFile("./client_config.toml"));
            }

            if (NeedsResourcePack)
            {
                GetResourceCache = IoCManager.Resolve<IResourceCache>();
                InitializeResources();
            }
        }

        #region Setup

        /// <summary>
        /// Registers all the types into the <see cref="IoCManager"/> with <see cref="IoCManager.Register{TInterface, TImplementation}"/>
        /// </summary>
        private void RegisterIoC()
        {
            // Shared stuff.
            IoCManager.Register<IComponentManager, ComponentManager>();
            IoCManager.Register<IPrototypeManager, PrototypeManager>();
            IoCManager.Register<IEntitySystemManager, EntitySystemManager>();
            IoCManager.Register<IConfigurationManager, ConfigurationManager>();
            IoCManager.Register<ISS14Serializer, SS14Serializer>();
            IoCManager.Register<INetManager, NetManager>();
            IoCManager.Register<IGameTiming, GameTiming>();
            IoCManager.Register<IResourceManager, ResourceManager>();

            switch (Project)
            {
                case UnitTestProject.Client:
                    IoCManager.Register<ILogManager, LogManager>();

                    IoCManager.Register<IRand, Rand>();
                    IoCManager.Register<IStateManager, StateManager>();
                    IoCManager.Register<INetworkGrapher, NetworkGrapher>();
                    IoCManager.Register<IKeyBindingManager, KeyBindingManager>();
                    IoCManager.Register<IUserInterfaceManager, UserInterfaceManager>();
                    IoCManager.Register<ITileDefinitionManager, TileDefinitionManager>();
                    IoCManager.Register<ICollisionManager, CollisionManager>();
                    IoCManager.Register<IEntityManager, ClientEntityManager>();
                    IoCManager.Register<IClientEntityManager, ClientEntityManager>();
                    IoCManager.Register<IClientNetManager, NetManager>();
                    IoCManager.Register<IReflectionManager, ClientReflectionManager>();
                    IoCManager.Register<IPlacementManager, PlacementManager>();
                    IoCManager.Register<ILightManager, LightManager>();
                    IoCManager.Register<IResourceCache, ResourceCache>();
                    IoCManager.Register<IMapManager, MapManager>();
                    IoCManager.Register<IEntityNetworkManager, ClientEntityNetworkManager>();
                    IoCManager.Register<IPlayerManager, PlayerManager>();
                    IoCManager.Register<IGameController, GameController>();
                    IoCManager.Register<IComponentFactory, ClientComponentFactory>();
                    break;

                case UnitTestProject.Server:
                    IoCManager.Register<IEntityManager, ServerEntityManager>();
                    IoCManager.Register<IServerEntityManager, ServerEntityManager>();
                    IoCManager.Register<ILogManager, ServerLogManager>();
                    IoCManager.Register<IServerLogManager, ServerLogManager>();
                    IoCManager.Register<IChatManager, ChatManager>();
                    IoCManager.Register<IServerNetManager, NetManager>();
                    IoCManager.Register<IMapManager, MapManager>();
                    IoCManager.Register<IPlacementManager, PlacementManager>();
                    IoCManager.Register<IConsoleManager, ConsoleManager>();
                    IoCManager.Register<ITileDefinitionManager, TileDefinitionManager>();
                    IoCManager.Register<IRoundManager, RoundManager>();
                    IoCManager.Register<IEntityNetworkManager, ServerEntityNetworkManager>();
                    IoCManager.Register<ICommandLineArgs, CommandLineArgs>();
                    IoCManager.Register<IGameStateManager, GameStateManager>();
                    IoCManager.Register<IReflectionManager, ServerReflectionManager>();
                    IoCManager.Register<IClientConsoleHost, SS14.Server.ClientConsoleHost.ClientConsoleHost>();
                    IoCManager.Register<IPlayerManager, PlayerManager>();
                    IoCManager.Register<IComponentFactory, ServerComponentFactory>();
                    IoCManager.Register<IBaseServer, BaseServer>();
                    IoCManager.Register<IMapLoader, MapLoader>();
                    break;

                default:
                    throw new NotSupportedException($"Unknown testing project: {Project}");
            }

            OverrideIoC();

            IoCManager.BuildGraph();
        }

        /// <summary>
        /// Called after all IoC registration has been done, but before the graph has been built.
        /// This allows one to add new IoC types or overwrite existing ones if needed.
        /// </summary>
        protected virtual void OverrideIoC()
        {
        }

        public void InitializeResources()
        {
            GetResourceCache.LoadBaseResources();
            GetResourceCache.LoadLocalResources();
        }

        public void InitializeCluwneLib()
        {
            GetClock = new Clock();

            CluwneLib.Video.SetWindowSize(1280, 720);
            CluwneLib.Video.SetFullScreen(false);
            CluwneLib.Video.SetRefreshRate(60);

            CluwneLib.Initialize();
            CluwneLib.Window.Graphics.BackgroundColor = Color.Black;
            CluwneLib.Window.Closed += MainWindowRequestClose;

            CluwneLib.Go();
        }

        public void InitializeCluwneLib(uint width, uint height, bool fullscreen, uint refreshrate)
        {
            GetClock = new Clock();

            CluwneLib.Video.SetWindowSize(width, height);
            CluwneLib.Video.SetFullScreen(fullscreen);
            CluwneLib.Video.SetRefreshRate(refreshrate);

            CluwneLib.Initialize();
            CluwneLib.Window.Graphics.BackgroundColor = Color.Black;
            CluwneLib.Window.Closed += MainWindowRequestClose;

            CluwneLib.Go();
        }

        public void StartCluwneLibLoop()
        {
            while (CluwneLib.IsRunning)
            {
                var lastFrameTime = GetClock.ElapsedTime.AsSeconds();
                GetClock.Restart();
                frameEvent = new FrameEventArgs(lastFrameTime);
                CluwneLib.ClearCurrentRendertarget(Color4.Black);
                CluwneLib.Window.DispatchEvents();
                InjectedMethod();
                CluwneLib.Window.Graphics.Display();
            }
        }

        private void MainWindowRequestClose(object sender, EventArgs e)
        {
            CluwneLib.Stop();
            Application.Exit();
        }

        #endregion Setup
    }
}

using Robust.Server.Bql;
using Robust.Server.Console;
using Robust.Server.DataMetrics;
using Robust.Server.Debugging;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Server.Maps;
using Robust.Server.Placement;
using Robust.Server.Player;
using Robust.Server.Prototypes;
using Robust.Server.Reflection;
using Robust.Server.Scripting;
using Robust.Server.ServerHub;
using Robust.Server.ServerStatus;
using Robust.Server.ViewVariables;
using Robust.Shared;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Players;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;
using Robust.Shared.Timing;
using Robust.Shared.ViewVariables;

namespace Robust.Server
{
    internal static class ServerIoC
    {
        /// <summary>
        /// Registers all the types into the <see cref="IDependencyCollection"/>
        /// </summary>
        internal static void RegisterIoC(IDependencyCollection deps)
        {
            SharedIoC.RegisterIoC(deps);

            IoCManager.Register<IBaseServer, BaseServer>();
            IoCManager.Register<IBaseServerInternal, BaseServer>();
            IoCManager.Register<BaseServer, BaseServer>();
            IoCManager.Register<IGameTiming, GameTiming>();
            IoCManager.Register<IReflectionManager, ServerReflectionManager>();
            IoCManager.Register<IConsoleHost, ServerConsoleHost>();
            IoCManager.Register<IServerConsoleHost, ServerConsoleHost>();
            IoCManager.Register<IComponentFactory, ServerComponentFactory>();
            IoCManager.Register<IConGroupController, ConGroupController>();
            IoCManager.Register<IMapManager, NetworkedMapManager>();
            IoCManager.Register<IMapManagerInternal, NetworkedMapManager>();
            IoCManager.Register<INetworkedMapManager, NetworkedMapManager>();
            IoCManager.Register<IEntityManager, ServerEntityManager>();
            IoCManager.Register<IEntityNetworkManager, ServerEntityManager>();
            IoCManager.Register<IServerEntityNetworkManager, ServerEntityManager>();
            IoCManager.Register<IPlacementManager, PlacementManager>();
            IoCManager.Register<IPlayerManager, PlayerManager>();
            IoCManager.Register<ISharedPlayerManager, PlayerManager>();
            IoCManager.Register<IPrototypeManager, ServerPrototypeManager>();
            IoCManager.Register<IResourceManager, ResourceManager>();
            IoCManager.Register<IResourceManagerInternal, ResourceManager>();
            IoCManager.Register<EntityManager, ServerEntityManager>();
            IoCManager.Register<IServerEntityManager, ServerEntityManager>();
            IoCManager.Register<IServerEntityManagerInternal, ServerEntityManager>();
            IoCManager.Register<IServerGameStateManager, ServerGameStateManager>();
            IoCManager.Register<IServerNetManager, NetManager>();
            IoCManager.Register<IStatusHost, StatusHost>();
            IoCManager.Register<ISystemConsoleManager, SystemConsoleManager>();
            IoCManager.Register<ITileDefinitionManager, TileDefinitionManager>();
            IoCManager.Register<IViewVariablesManager, ServerViewVariablesManager>();
            IoCManager.Register<IServerViewVariablesInternal, ServerViewVariablesManager>();
            IoCManager.Register<IWatchdogApi, WatchdogApi>();
            IoCManager.Register<IScriptHost, ScriptHost>();
            IoCManager.Register<IMetricsManager, MetricsManager>();
            IoCManager.Register<IAuthManager, AuthManager>();
            IoCManager.Register<IPhysicsManager, PhysicsManager>();
            IoCManager.Register<IBqlQueryManager, BqlQueryManager>();
            IoCManager.Register<HubManager, HubManager>();
        }
    }
}

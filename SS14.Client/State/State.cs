using SS14.Client.Interfaces.Configuration;
using SS14.Client.Interfaces.Input;
using SS14.Client.Interfaces.Map;
using SS14.Client.Interfaces.Network;
using SS14.Client.Interfaces.Placement;
using SS14.Client.Interfaces.Player;
using SS14.Client.Interfaces.Resource;
using SS14.Client.Interfaces.State;
using SS14.Client.Interfaces.UserInterface;
using System;
using System.Collections.Generic;

namespace SS14.Client.State
{
    public abstract class State
    {
        protected readonly IPlayerConfigurationManager ConfigurationManager;
        protected readonly IKeyBindingManager KeyBindingManager;
        protected readonly IMapManager MapManager;
        protected readonly INetworkManager NetworkManager;
        protected readonly IPlacementManager PlacementManager;
        protected readonly IPlayerManager PlayerManager;
        protected readonly IResourceManager ResourceManager;
        protected readonly IStateManager StateManager;
        protected readonly IUserInterfaceManager UserInterfaceManager;

        protected State(IDictionary<Type, object> managers)
        {
            StateManager = (IStateManager) managers[typeof (IStateManager)];
            NetworkManager = (INetworkManager) managers[typeof (INetworkManager)];
            ResourceManager = (IResourceManager) managers[typeof (IResourceManager)];
            UserInterfaceManager = (IUserInterfaceManager) managers[typeof (IUserInterfaceManager)];
            MapManager = (IMapManager) managers[typeof (IMapManager)];
            PlayerManager = (IPlayerManager) managers[typeof (IPlayerManager)];
            ConfigurationManager = (IPlayerConfigurationManager) managers[typeof (IPlayerConfigurationManager)];
            PlacementManager = (IPlacementManager) managers[typeof (IPlacementManager)];
            KeyBindingManager = (IKeyBindingManager) managers[typeof (IKeyBindingManager)];
        }
    }
}
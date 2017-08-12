﻿using SFML.Window;
using SS14.Client.Graphics.Event;
using SS14.Client.Interfaces.Input;
using SS14.Client.Interfaces.Map;
using SS14.Client.Interfaces.Placement;
using SS14.Client.Interfaces.Player;
using SS14.Client.Interfaces.Resource;
using SS14.Client.Interfaces.State;
using SS14.Client.Interfaces.UserInterface;
using SS14.Shared;
using SS14.Shared.IoC;
using SS14.Shared.Interfaces.Configuration;
using System;
using System.Collections.Generic;
using SS14.Shared.Interfaces.Network;
using KeyEventArgs = SFML.Window.KeyEventArgs;

namespace SS14.Client.State
{
    public class StateManager : IStateManager, IPostInjectInit
    {
        [Dependency]
        private readonly IConfigurationManager configurationManager;
        [Dependency]
        private readonly IClientNetManager networkManager;
        [Dependency]
        private readonly IUserInterfaceManager userInterfaceManager;
        [Dependency]
        private readonly IResourceCache resourceCache;
        [Dependency]
        private readonly IMapManager mapManager;
        [Dependency]
        private readonly IPlayerManager playerManager;
        [Dependency]
        private readonly IPlacementManager placementManager;
        [Dependency]
        private readonly IKeyBindingManager keyBindingManager;

        private readonly Dictionary<Type, IState> _loadedStates = new Dictionary<Type, IState>();
        private readonly Dictionary<Type, object> _managers = new Dictionary<Type, object>();

        #region IStateManager Members

        public IState CurrentState { get; private set; } = null;

        #endregion

        public void PostInject()
        {
            _managers[typeof(IClientNetManager)] = networkManager;
            _managers[typeof(IUserInterfaceManager)] = userInterfaceManager;
            _managers[typeof(IResourceCache)] = resourceCache;
            _managers[typeof(IMapManager)] = mapManager;
            _managers[typeof(IPlayerManager)] = playerManager;
            _managers[typeof(IPlacementManager)] = placementManager;
            _managers[typeof(IKeyBindingManager)] = keyBindingManager;
            _managers[typeof(IConfigurationManager)] = configurationManager;
            _managers[typeof(IStateManager)] = this;

            playerManager.RequestedStateSwitch += HandleStateChange;
        }

        #region Input

        public void KeyDown(KeyEventArgs e)
        {
            if (CurrentState != null)
                CurrentState.KeyDown(e);
        }

        public void KeyUp(KeyEventArgs e)
        {
            if (CurrentState != null)
                CurrentState.KeyUp(e);
        }

        public void MouseUp(MouseButtonEventArgs e)
        {
            if (CurrentState != null)
                CurrentState.MouseUp(e);
        }

        public void MouseDown(MouseButtonEventArgs e)
        {
            if (CurrentState != null)
                CurrentState.MouseDown(e);
        }

        public void MouseMove(MouseMoveEventArgs e)
        {
            if (CurrentState != null)
                CurrentState.MouseMove(e);
        }

        public void MouseWheelMove(MouseWheelEventArgs e)
        {
            if (CurrentState != null)
                CurrentState.MouseWheelMove(e);
        }

        public void MouseEntered(EventArgs e)
        {
            if (CurrentState != null)
                CurrentState.MouseEntered(e);
        }

        public void MouseLeft(EventArgs e)
        {
            if (CurrentState != null)
                CurrentState.MouseLeft(e);
        }

        public void TextEntered(TextEventArgs e)
        {
            if (CurrentState != null)
                CurrentState.TextEntered(e);
        }

        #endregion

        #region Updates & Statechanges

        public void Update(FrameEventArgs e)
        {
            CurrentState?.Update(e);
        }

        public void Render(FrameEventArgs e)
        {
            CurrentState?.Render(e);
        }

        public void RequestStateChange<T>() where T : IState
        {
            if (CurrentState == null || CurrentState.GetType() != typeof (T))
                SwitchToState<T>();
        }

        public void FormResize()
        {
            if (CurrentState == null)
                return;

            CurrentState.FormResize();
        }

        private void SwitchToState<T>() where T : IState
        {
            IState newState;

            if (_loadedStates.ContainsKey(typeof (T)))
            {
                newState = (T) _loadedStates[typeof (T)];
            }
            else
            {
                var parameters = new object[] {_managers};
                newState = (T) Activator.CreateInstance(typeof (T), parameters);
                _loadedStates.Add(typeof (T), newState);
            }

            if (CurrentState != null) CurrentState.Shutdown();

            CurrentState = newState;
            CurrentState.Startup();
        }

        private void RequestStateChange(Type type)
        {
            if (CurrentState.GetType() != type)
            {
                SwitchToState(type);
            }
        }

        private void SwitchToState(Type type)
        {
            IState newState;

            if (_loadedStates.ContainsKey(type))
            {
                newState = _loadedStates[type];
            }
            else
            {
                var parameters = new object[] {_managers};
                newState = (IState) Activator.CreateInstance(type, parameters);
                _loadedStates.Add(type, newState);
            }

            if (CurrentState != null) CurrentState.Shutdown();

            CurrentState = newState;
            CurrentState.Startup();
        }

        private void HandleStateChange(object sender, TypeEventArgs args)
        {
            if (args.Type.GetInterface("IState") != null)
            {
                RequestStateChange(args.Type);
            }
        }

        #endregion
    }
}
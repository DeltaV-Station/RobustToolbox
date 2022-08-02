using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Utility;
using Robust.Shared.ViewVariables;

namespace Robust.Server.GameObjects
{
    [UsedImplicitly]
    public sealed class UserInterfaceSystem : SharedUserInterfaceSystem
    {
        private const float MaxWindowRange = 2;
        private const float MaxWindowRangeSquared = MaxWindowRange * MaxWindowRange;

        private readonly List<IPlayerSession> _sessionCache = new();

        private Dictionary<IPlayerSession, List<BoundUserInterface>> _openInterfaces = new();

        // List of all bound user interfaces that have at least one player looking at them.
        [ViewVariables]
        private readonly HashSet<BoundUserInterface> _activeInterfaces = new();

        [Dependency] private readonly IPlayerManager _playerMan = default!;

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            SubscribeNetworkEvent<BoundUIWrapMessage>(OnMessageReceived);
            SubscribeLocalEvent<ServerUserInterfaceComponent, ComponentShutdown>(OnUserInterfaceShutdown);
            _playerMan.PlayerStatusChanged += OnPlayerStatusChanged;
        }

        public override void Shutdown()
        {
            base.Shutdown();

            _playerMan.PlayerStatusChanged -= OnPlayerStatusChanged;
        }

        private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
        {
            if (args.NewStatus != SessionStatus.Disconnected)
                return;

            if (!_openInterfaces.TryGetValue(args.Session, out var buis))
                return;

            foreach (var bui in buis)
            {
                CloseShared(bui, args.Session);
            }
        }

        private void OnUserInterfaceShutdown(EntityUid uid, ServerUserInterfaceComponent component, ComponentShutdown args)
        {
            foreach (var bui in component.Interfaces)
            {
                DeactivateInterface(bui);
            }
        }

        /// <summary>
        ///     Validates the received message, and then pass it onto systems/components
        /// </summary>
        private void OnMessageReceived(BoundUIWrapMessage msg, EntitySessionEventArgs args)
        {
            var uid = msg.Entity;
            if (!TryComp(uid, out ServerUserInterfaceComponent? uiComp) || args.SenderSession is not IPlayerSession session)
                return;

            if (!uiComp.TryGetBoundUserInterface(msg.UiKey, out var ui))
            {
                Logger.DebugS("go.comp.ui", "Got BoundInterfaceMessageWrapMessage for unknown UI key: {0}", msg.UiKey);
                return;
            }

            if (!ui.SubscribedSessions.Contains(session))
            {
                Logger.DebugS("go.comp.ui", $"UI {msg.UiKey} got BoundInterfaceMessageWrapMessage from a client who was not subscribed: {session}", msg.UiKey);
                return;
            }

            // if they want to close the UI, we can go home early.
            if (msg.Message is CloseBoundInterfaceMessage)
            {
                CloseShared(ui, session);
                return;
            }

            // verify that the user is allowed to press buttons on this UI:
            if (ui.RequireInputValidation)
            {
                var attempt = new BoundUserInterfaceMessageAttempt(args.SenderSession, uid, msg.UiKey);
                RaiseLocalEvent(attempt);
                if (attempt.Cancelled)
                    return;
            }

            // get the wrapped message and populate it with the sender & UI key information.
            var message = msg.Message;
            message.Session = args.SenderSession;
            message.Entity = uid;
            message.UiKey = msg.UiKey;

            // Raise as object so the correct type is used.
            RaiseLocalEvent(uid, (object)message, true);

            // Once we have populated our message's wrapped message, we will wrap it up into a message that can be sent
            // to old component-code.
            var WrappedUnwrappedMessageMessageMessage = new ServerBoundUserInterfaceMessage(message, session);
            ui.InvokeOnReceiveMessage(WrappedUnwrappedMessageMessageMessage);
        }

        /// <inheritdoc />
        public override void Update(float frameTime)
        {
            foreach (var ui in _activeInterfaces.ToList())
            {
                CheckRange(ui);

                if (!ui.StateDirty)
                    continue;

                ui.StateDirty = false;

                foreach (var (player, state) in ui.PlayerStateOverrides)
                {
                    RaiseNetworkEvent(state, player.ConnectedClient);
                }

                if (ui.LastStateMsg == null)
                    continue;

                foreach (var session in ui.SubscribedSessions)
                {
                    if (!ui.PlayerStateOverrides.ContainsKey(session))
                        RaiseNetworkEvent(ui.LastStateMsg, session.ConnectedClient);
                }
            }
        }

        /// <summary>
        ///     Verify that the subscribed clients are still in range of the interface.
        /// </summary>
        private void CheckRange(BoundUserInterface ui)
        {
            // We have to cache the set of sessions because Unsubscribe modifies the original.
            _sessionCache.Clear();
            _sessionCache.AddRange(ui.SubscribedSessions);

            var transform = EntityManager.GetComponent<TransformComponent>(ui.Component.Owner);

            var uiPos = transform.WorldPosition;
            var uiMap = transform.MapID;

            foreach (var session in _sessionCache)
            {
                var attachedEntityTransform = session.AttachedEntityTransform;

                // The component manages the set of sessions, so this invalid session should be removed soon.
                if (attachedEntityTransform == null)
                {
                    continue;
                }

                if (uiMap != attachedEntityTransform.MapID)
                {
                    CloseUi(ui, session);
                    continue;
                }

                var distanceSquared = (uiPos - attachedEntityTransform.WorldPosition).LengthSquared;
                if (distanceSquared > MaxWindowRangeSquared)
                {
                    CloseUi(ui, session);
                }
            }
        }

        private void DeactivateInterface(BoundUserInterface userInterface)
        {
            _activeInterfaces.Remove(userInterface);
        }

        private void ActivateInterface(BoundUserInterface userInterface)
        {
            _activeInterfaces.Add(userInterface);
        }

        #region Get BUI
        public bool HasUi(EntityUid uid, object uiKey, ServerUserInterfaceComponent? ui = null)
        {
            if (!Resolve(uid, ref ui))
                return false;

            return ui._interfaces.ContainsKey(uiKey);
        }

        public BoundUserInterface GetUi(EntityUid uid, object uiKey, ServerUserInterfaceComponent? ui = null)
        {
            if (!Resolve(uid, ref ui))
                throw new InvalidOperationException($"Cannot get {typeof(BoundUserInterface)} from an entity without {typeof(ServerUserInterfaceComponent)}!");

            return ui._interfaces[uiKey];
        }

        public BoundUserInterface? GetUiOrNull(EntityUid uid, object uiKey, ServerUserInterfaceComponent? ui = null)
        {
            return TryGetUi(uid, uiKey, out var bui, ui)
                ? bui
                : null;
        }

        public bool TryGetUi(EntityUid uid, object uiKey, [NotNullWhen(true)] out BoundUserInterface? bui, ServerUserInterfaceComponent? ui = null)
        {
            bui = null;

            return Resolve(uid, ref ui, false) && ui.TryGetBoundUserInterface(uiKey, out bui);
        }
        #endregion

        public bool IsUiOpen(EntityUid uid, object uiKey, ServerUserInterfaceComponent? ui = null)
        {
            if (!TryGetUi(uid, uiKey, out var bui, ui))
                return false;

            return bui.SubscribedSessions.Count > 0;
        }

        public bool SessionHasOpenUi(EntityUid uid, object uiKey, IPlayerSession session, ServerUserInterfaceComponent? ui = null)
        {
            if (!TryGetUi(uid, uiKey, out var bui, ui))
                return false;

            return bui.SubscribedSessions.Contains(session);
        }

        /// <summary>
        ///     Sets a state. This can be used for stateful UI updating.
        ///     This state is sent to all clients, and automatically sent to all new clients when they open the UI.
        ///     Pretty much how NanoUI did it back in ye olde BYOND.
        /// </summary>
        /// <param name="state">
        ///     The state object that will be sent to all current and future client.
        ///     This can be null.
        /// </param>
        /// <param name="session">
        ///     The player session to send this new state to.
        ///     Set to null for sending it to every subscribed player session.
        /// </param>
        public bool TrySetUiState(EntityUid uid,
            object uiKey,
            BoundUserInterfaceState state,
            IPlayerSession? session = null,
            ServerUserInterfaceComponent? ui = null,
            bool clearOverrides = true)
        {
            if (!TryGetUi(uid, uiKey, out var bui, ui))
                return false;

            SetUiState(bui, state, session, clearOverrides);
            return true;
        }

        /// <summary>
        ///     Sets a state. This can be used for stateful UI updating.
        ///     This state is sent to all clients, and automatically sent to all new clients when they open the UI.
        ///     Pretty much how NanoUI did it back in ye olde BYOND.
        /// </summary>
        /// <param name="state">
        ///     The state object that will be sent to all current and future client.
        ///     This can be null.
        /// </param>
        /// <param name="session">
        ///     The player session to send this new state to.
        ///     Set to null for sending it to every subscribed player session.
        /// </param>
        public void SetUiState(BoundUserInterface bui, BoundUserInterfaceState state, IPlayerSession? session = null, bool clearOverrides = true)
        {
            var msg = new BoundUIWrapMessage(bui.Component.Owner, new UpdateBoundStateMessage(state), bui.UiKey);
            if (session == null)
            {
                bui.LastStateMsg = msg;
                if (clearOverrides)
                    bui.PlayerStateOverrides.Clear();
            }
            else
            {
                bui.PlayerStateOverrides[session] = msg;
            }

            bui.StateDirty = true;
        }

        /// <summary>
        ///     Switches between closed and open for a specific client.
        /// </summary>
        public bool TryToggleUi(EntityUid uid, object uiKey, IPlayerSession session, ServerUserInterfaceComponent? ui = null)
        {
            if (!TryGetUi(uid, uiKey, out var bui, ui))
                return false;

            ToggleUi(bui, session);
            return true;
        }

        /// <summary>
        ///     Switches between closed and open for a specific client.
        /// </summary>
        public void ToggleUi(BoundUserInterface bui, IPlayerSession session)
        {
            if (bui._subscribedSessions.Contains(session))
                CloseUi(bui, session);
            else
                OpenUi(bui, session);
        }

        #region Open

        public bool TryOpen(EntityUid uid, object uiKey, IPlayerSession session, ServerUserInterfaceComponent? ui = null)
        {
            if (!TryGetUi(uid, uiKey, out var bui, ui))
                return false;

            return OpenUi(bui, session);
        }

        /// <summary>
        ///     Opens this interface for a specific client.
        /// </summary>
        public bool OpenUi(BoundUserInterface bui, IPlayerSession session)
        {
            if (session.Status == SessionStatus.Connecting || session.Status == SessionStatus.Disconnected)
                return false;

            if (!bui._subscribedSessions.Add(session))
                return false;

            _openInterfaces.GetOrNew(session).Add(bui);
            RaiseLocalEvent(bui.Component.Owner, new BoundUIOpenedEvent(bui.UiKey, bui.Component.Owner, session));

            if (bui.LastStateMsg != null)
                RaiseNetworkEvent(bui.LastStateMsg, session.ConnectedClient);

            RaiseNetworkEvent(new BoundUIWrapMessage(bui.Component.Owner, new OpenBoundInterfaceMessage(), bui.UiKey), session.ConnectedClient);
            ActivateInterface(bui);
            return true;
        }

        #endregion

        #region Close
        public bool TryClose(EntityUid uid, object uiKey, IPlayerSession session, ServerUserInterfaceComponent? ui = null)
        {
            if (!TryGetUi(uid, uiKey, out var bui, ui))
                return false;

            return CloseUi(bui, session);
        }

        /// <summary>
        ///     Close this interface for a specific client.
        /// </summary>
        public bool CloseUi(BoundUserInterface bui, IPlayerSession session)
        {
            if (!bui._subscribedSessions.Remove(session))
                return false;

            RaiseNetworkEvent(new BoundUIWrapMessage(bui.Component.Owner, new CloseBoundInterfaceMessage(), bui.UiKey), session.ConnectedClient);
            CloseShared(bui, session);
            return true;
        }

        private void CloseShared(BoundUserInterface bui, IPlayerSession session)
        {
            var owner = bui.Component.Owner;
            bui._subscribedSessions.Remove(session);
            bui.PlayerStateOverrides.Remove(session);

            if (_openInterfaces.TryGetValue(session, out var buis))
                buis.Remove(bui);

            bui.InvokeOnClosed(session);
            RaiseLocalEvent(owner, new BoundUIClosedEvent(bui.UiKey, owner, session));

            if (bui._subscribedSessions.Count == 0)
                DeactivateInterface(bui);
        }

        /// <summary>
        ///     Closes this interface for any clients that have it open.
        /// </summary>
        public bool TryCloseAll(EntityUid uid, object uiKey, ServerUserInterfaceComponent? ui = null)
        {
            if (!TryGetUi(uid, uiKey, out var bui, ui))
                return false;

            CloseAll(bui);
            return true;
        }

        /// <summary>
        ///     Closes this interface for any clients that have it open.
        /// </summary>
        public void CloseAll(BoundUserInterface bui)
        {
            foreach (var session in bui.SubscribedSessions.ToArray())
            {
                CloseUi(bui, session);
            }
        }
        #endregion

        #region SendMessage
        /// <summary>
        ///     Send a BUI message to all connected player sessions.
        /// </summary>
        public bool TrySendUiMessage(EntityUid uid, object uiKey, BoundUserInterfaceMessage message, ServerUserInterfaceComponent? ui = null)
        {
            if (!TryGetUi(uid, uiKey, out var bui, ui))
                return false;

            SendUiMessage(bui, message);
            return true;
        }

        /// <summary>
        ///     Send a BUI message to all connected player sessions.
        /// </summary>
        public void SendUiMessage(BoundUserInterface bui, BoundUserInterfaceMessage message)
        {
            var msg = new BoundUIWrapMessage(bui.Component.Owner, message, bui.UiKey);
            foreach (var session in bui.SubscribedSessions)
            {
                RaiseNetworkEvent(msg, session.ConnectedClient);
            }
        }

        /// <summary>
        ///     Send a BUI message to a specific player session.
        /// </summary>
        public bool TrySendUiMessage(EntityUid uid, object uiKey, BoundUserInterfaceMessage message, IPlayerSession session, ServerUserInterfaceComponent? ui = null)
        {
            if (!TryGetUi(uid, uiKey, out var bui, ui))
                return false;

            return TrySendUiMessage(bui, message, session);
        }

        /// <summary>
        ///     Send a BUI message to a specific player session.
        /// </summary>
        public bool TrySendUiMessage(BoundUserInterface bui, BoundUserInterfaceMessage message, IPlayerSession session)
        {
            if (!bui.SubscribedSessions.Contains(session))
                return false;

            RaiseNetworkEvent(new BoundUIWrapMessage(bui.Component.Owner, message, bui.UiKey), session.ConnectedClient);
            return true;
        }

        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Utility;

namespace Robust.Server.GameObjects
{
    [UsedImplicitly]
    public sealed class UserInterfaceSystem : SharedUserInterfaceSystem
    {
        [Dependency] private readonly IPlayerManager _playerMan = default!;
        [Dependency] private readonly TransformSystem _xformSys = default!;

        private EntityQuery<IgnoreUIRangeComponent> _ignoreUIRangeQuery;

        private readonly List<IPlayerSession> _sessionCache = new();

        private readonly Dictionary<IPlayerSession, List<BoundUserInterface>> _openInterfaces = new();

        /// <inheritdoc />
        public override void Initialize()
        {
            base.Initialize();

            SubscribeNetworkEvent<BoundUIWrapMessage>(OnMessageReceived);
            SubscribeLocalEvent<ServerUserInterfaceComponent, ComponentInit>(OnUserInterfaceInit);
            SubscribeLocalEvent<ServerUserInterfaceComponent, ComponentShutdown>(OnUserInterfaceShutdown);
            _playerMan.PlayerStatusChanged += OnPlayerStatusChanged;

            _ignoreUIRangeQuery = GetEntityQuery<IgnoreUIRangeComponent>();
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

            foreach (var bui in buis.ToArray())
            {
                CloseShared(bui, args.Session);
            }
        }

        private void OnUserInterfaceInit(EntityUid uid, ServerUserInterfaceComponent component, ComponentInit args)
        {
            component.Interfaces.Clear();

            foreach (var prototypeData in component._interfaceData)
            {
                component.Interfaces[prototypeData.UiKey] = new BoundUserInterface(prototypeData, uid);
            }
        }

        private void OnUserInterfaceShutdown(EntityUid uid, ServerUserInterfaceComponent component, ComponentShutdown args)
        {
            if (!TryComp(uid, out ActiveUserInterfaceComponent? activeUis))
                return;

            foreach (var bui in activeUis.Interfaces)
            {
                DeactivateInterface(uid, bui, activeUis);
            }
        }

        /// <summary>
        ///     Validates the received message, and then pass it onto systems/components
        /// </summary>
        private void OnMessageReceived(BoundUIWrapMessage msg, EntitySessionEventArgs args)
        {
            var uid = GetEntity(msg.Entity);
            if (!TryComp(uid, out ServerUserInterfaceComponent? uiComp) || args.SenderSession is not IPlayerSession session)
                return;

            if (!uiComp.Interfaces.TryGetValue(msg.UiKey, out var ui))
            {
                Log.Debug($"Got BoundInterfaceMessageWrapMessage for unknown UI key: {msg.UiKey}");
                return;
            }

            if (!ui.SubscribedSessions.Contains(session))
            {
                Log.Debug($"UI {msg.UiKey} got BoundInterfaceMessageWrapMessage from a client who was not subscribed: {session}");
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
            message.Entity = GetNetEntity(uid);
            message.UiKey = msg.UiKey;

            // Raise as object so the correct type is used.
            RaiseLocalEvent(uid, (object)message, true);
        }

        /// <inheritdoc />
        public override void Update(float frameTime)
        {
            var xformQuery = GetEntityQuery<TransformComponent>();
            var query = AllEntityQuery<ActiveUserInterfaceComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var activeUis, out var xform))
            {
                foreach (var ui in activeUis.Interfaces)
                {
                    CheckRange(uid, activeUis, ui, xform, xformQuery);

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
        }

        /// <summary>
        ///     Verify that the subscribed clients are still in range of the interface.
        /// </summary>
        private void CheckRange(EntityUid uid, ActiveUserInterfaceComponent activeUis, BoundUserInterface ui, TransformComponent transform, EntityQuery<TransformComponent> query)
        {
            if (ui.InteractionRange <= 0)
                return;

            // We have to cache the set of sessions because Unsubscribe modifies the original.
            _sessionCache.Clear();
            _sessionCache.AddRange(ui.SubscribedSessions);

            var uiPos = _xformSys.GetWorldPosition(transform, query);
            var uiMap = transform.MapID;

            foreach (var session in _sessionCache)
            {
                // The component manages the set of sessions, so this invalid session should be removed soon.
                if (!query.TryGetComponent(session.AttachedEntity, out var xform))
                    continue;

                if (_ignoreUIRangeQuery.HasComponent(session.AttachedEntity))
                    continue;

                // Handle pluggable BoundUserInterfaceCheckRangeEvent
                var checkRangeEvent = new BoundUserInterfaceCheckRangeEvent(uid, ui, session);
                RaiseLocalEvent(uid, ref checkRangeEvent, broadcast: true);
                if (checkRangeEvent.Result == BoundUserInterfaceRangeResult.Pass)
                    continue;

                if (checkRangeEvent.Result == BoundUserInterfaceRangeResult.Fail)
                {
                    CloseUi(ui, session, activeUis);
                    continue;
                }

                DebugTools.Assert(checkRangeEvent.Result == BoundUserInterfaceRangeResult.Default);

                if (uiMap != xform.MapID)
                {
                    CloseUi(ui, session, activeUis);
                    continue;
                }

                var distanceSquared = (uiPos - _xformSys.GetWorldPosition(xform, query)).LengthSquared();
                if (distanceSquared > ui.InteractionRangeSqrd)
                    CloseUi(ui, session, activeUis);
            }
        }

        private void DeactivateInterface(EntityUid entityUid, BoundUserInterface ui,
            ActiveUserInterfaceComponent? activeUis = null)
        {
            if (!Resolve(entityUid, ref activeUis, false))
                return;

            activeUis.Interfaces.Remove(ui);
            if (activeUis.Interfaces.Count == 0)
                RemCompDeferred(entityUid, activeUis);
        }

        private void ActivateInterface(BoundUserInterface ui)
        {
            EnsureComp<ActiveUserInterfaceComponent>(ui.Owner).Interfaces.Add(ui);
        }

        #region Get BUI
        public bool HasUi(EntityUid uid, Enum uiKey, ServerUserInterfaceComponent? ui = null)
        {
            if (!Resolve(uid, ref ui))
                return false;

            return ui.Interfaces.ContainsKey(uiKey);
        }

        public BoundUserInterface GetUi(EntityUid uid, Enum uiKey, ServerUserInterfaceComponent? ui = null)
        {
            if (!Resolve(uid, ref ui))
                throw new InvalidOperationException($"Cannot get {typeof(BoundUserInterface)} from an entity without {typeof(ServerUserInterfaceComponent)}!");

            return ui.Interfaces[uiKey];
        }

        public BoundUserInterface? GetUiOrNull(EntityUid uid, Enum uiKey, ServerUserInterfaceComponent? ui = null)
        {
            return TryGetUi(uid, uiKey, out var bui, ui)
                ? bui
                : null;
        }
        public bool TryGetUi(EntityUid uid, Enum uiKey, [NotNullWhen(true)] out BoundUserInterface? bui, ServerUserInterfaceComponent? ui = null)
        {
            bui = null;

            return Resolve(uid, ref ui, false) && ui.Interfaces.TryGetValue(uiKey, out bui);
        }

        /// <summary>
        ///     Return UIs a session has open.
        ///     Null if empty.
        /// <summary>
        public List<BoundUserInterface>? GetAllUIsForSession(IPlayerSession session)
        {
            _openInterfaces.TryGetValue(session, out var value);
            return value;
        }
        #endregion

        public bool IsUiOpen(EntityUid uid, Enum uiKey, ServerUserInterfaceComponent? ui = null)
        {
            if (!TryGetUi(uid, uiKey, out var bui, ui))
                return false;

            return bui.SubscribedSessions.Count > 0;
        }

        public bool SessionHasOpenUi(EntityUid uid, Enum uiKey, IPlayerSession session, ServerUserInterfaceComponent? ui = null)
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
            Enum uiKey,
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
            var msg = new BoundUIWrapMessage(GetNetEntity(bui.Owner), new UpdateBoundStateMessage(state), bui.UiKey);
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
        public bool TryToggleUi(EntityUid uid, Enum uiKey, IPlayerSession session, ServerUserInterfaceComponent? ui = null)
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

        public bool TryOpen(EntityUid uid, Enum uiKey, IPlayerSession session, ServerUserInterfaceComponent? ui = null)
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
            RaiseLocalEvent(bui.Owner, new BoundUIOpenedEvent(bui.UiKey, GetNetEntity(bui.Owner), session));

            RaiseNetworkEvent(new BoundUIWrapMessage(GetNetEntity(bui.Owner), new OpenBoundInterfaceMessage(), bui.UiKey), session.ConnectedClient);

            // Fun fact, clients needs to have BUIs open before they can receive the state.....
            if (bui.LastStateMsg != null)
                RaiseNetworkEvent(bui.LastStateMsg, session.ConnectedClient);

            ActivateInterface(bui);
            return true;
        }

        #endregion

        #region Close
        public bool TryClose(EntityUid uid, Enum uiKey, IPlayerSession session, ServerUserInterfaceComponent? ui = null)
        {
            if (!TryGetUi(uid, uiKey, out var bui, ui))
                return false;

            return CloseUi(bui, session);
        }

        /// <summary>
        ///     Close this interface for a specific client.
        /// </summary>
        public bool CloseUi(BoundUserInterface bui, IPlayerSession session, ActiveUserInterfaceComponent? activeUis = null)
        {
            if (!bui._subscribedSessions.Remove(session))
                return false;

            RaiseNetworkEvent(new BoundUIWrapMessage(GetNetEntity(bui.Owner), new CloseBoundInterfaceMessage(), bui.UiKey), session.ConnectedClient);
            CloseShared(bui, session, activeUis);
            return true;
        }

        private void CloseShared(BoundUserInterface bui, IPlayerSession session, ActiveUserInterfaceComponent? activeUis = null)
        {
            var owner = bui.Owner;
            bui._subscribedSessions.Remove(session);
            bui.PlayerStateOverrides.Remove(session);

            if (_openInterfaces.TryGetValue(session, out var buis))
                buis.Remove(bui);

            RaiseLocalEvent(owner, new BoundUIClosedEvent(bui.UiKey, GetNetEntity(owner), session));

            if (bui._subscribedSessions.Count == 0)
                DeactivateInterface(bui.Owner, bui, activeUis);
        }

        /// <summary>
        ///     Closes this all interface for any clients that have any open.
        /// </summary>
        public bool TryCloseAll(EntityUid uid, ActiveUserInterfaceComponent? aui = null)
        {
            if (!Resolve(uid, ref aui, false))
                return false;

            foreach (var ui in aui.Interfaces)
            {
                CloseAll(ui);
            }

            return true;
        }

        /// <summary>
        ///     Closes this specific interface for any clients that have it open.
        /// </summary>
        public bool TryCloseAll(EntityUid uid, Enum uiKey, ServerUserInterfaceComponent? ui = null)
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
        public bool TrySendUiMessage(EntityUid uid, Enum uiKey, BoundUserInterfaceMessage message, ServerUserInterfaceComponent? ui = null)
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
            var msg = new BoundUIWrapMessage(GetNetEntity(bui.Owner), message, bui.UiKey);
            foreach (var session in bui.SubscribedSessions)
            {
                RaiseNetworkEvent(msg, session.ConnectedClient);
            }
        }

        /// <summary>
        ///     Send a BUI message to a specific player session.
        /// </summary>
        public bool TrySendUiMessage(EntityUid uid, Enum uiKey, BoundUserInterfaceMessage message, IPlayerSession session, ServerUserInterfaceComponent? ui = null)
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

            RaiseNetworkEvent(new BoundUIWrapMessage(GetNetEntity(bui.Owner), message, bui.UiKey), session.ConnectedClient);
            return true;
        }

        #endregion
    }

    /// <summary>
    /// Raised by <see cref="UserInterfaceSystem"/> to check whether an interface is still accessible by its user.
    /// </summary>
    [ByRefEvent]
    [PublicAPI]
    public struct BoundUserInterfaceCheckRangeEvent
    {
        /// <summary>
        /// The entity owning the UI being checked for.
        /// </summary>
        public readonly EntityUid Target;

        /// <summary>
        /// The UI itself.
        /// </summary>
        /// <returns></returns>
        public readonly BoundUserInterface UserInterface;

        /// <summary>
        /// The player for which the UI is being checked.
        /// </summary>
        public readonly IPlayerSession Player;

        /// <summary>
        /// The result of the range check.
        /// </summary>
        public BoundUserInterfaceRangeResult Result;

        public BoundUserInterfaceCheckRangeEvent(
            EntityUid target,
            BoundUserInterface userInterface,
            IPlayerSession player)
        {
            Target = target;
            UserInterface = userInterface;
            Player = player;
        }
    }

    /// <summary>
    /// Possible results for a <see cref="BoundUserInterfaceCheckRangeEvent"/>.
    /// </summary>
    public enum BoundUserInterfaceRangeResult : byte
    {
        /// <summary>
        /// Run built-in range check.
        /// </summary>
        Default,

        /// <summary>
        /// Range check passed, UI is accessible.
        /// </summary>
        Pass,

        /// <summary>
        /// Range check failed, UI is inaccessible.
        /// </summary>
        Fail
    }
}

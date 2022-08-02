using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager.Attributes;
using static Robust.Shared.GameObjects.SharedUserInterfaceComponent;

namespace Robust.Server.GameObjects
{
    /// <summary>
    ///     Contains a collection of entity-bound user interfaces that can be opened per client.
    ///     Bound user interfaces are indexed with an enum or string key identifier.
    /// </summary>
    /// <seealso cref="BoundUserInterface"/>
    [PublicAPI]
    [ComponentReference(typeof(SharedUserInterfaceComponent))]
    public sealed class ServerUserInterfaceComponent : SharedUserInterfaceComponent, ISerializationHooks
    {
        internal readonly Dictionary<object, BoundUserInterface> _interfaces =
            new();

        [DataField("interfaces", readOnly: true)]
        private List<PrototypeData> _interfaceData = new();

        /// <summary>
        ///     Enumeration of all the interfaces this component provides.
        /// </summary>
        public IEnumerable<BoundUserInterface> Interfaces => _interfaces.Values;

        void ISerializationHooks.AfterDeserialization()
        {
            _interfaces.Clear();

            foreach (var prototypeData in _interfaceData)
            {
                _interfaces[prototypeData.UiKey] = new BoundUserInterface(prototypeData, this);
            }
        }

        public bool TryGetBoundUserInterface(object uiKey,
            [NotNullWhen(true)] out BoundUserInterface? boundUserInterface)
        {
            return _interfaces.TryGetValue(uiKey, out boundUserInterface);
        }

        public BoundUserInterface? GetBoundUserInterfaceOrNull(object uiKey)
        {
            return TryGetBoundUserInterface(uiKey, out var boundUserInterface)
                ? boundUserInterface
                : null;
        }
    }

    /// <summary>
    ///     Represents an entity-bound interface that can be opened by multiple players at once.
    /// </summary>
    [PublicAPI]
    public sealed class BoundUserInterface
    {
        public object UiKey { get; }
        public ServerUserInterfaceComponent Component { get; }
        internal readonly HashSet<IPlayerSession> _subscribedSessions = new();
        internal BoundUIWrapMessage? LastStateMsg;
        public bool RequireInputValidation;

        internal bool StateDirty;

        internal readonly Dictionary<IPlayerSession, BoundUIWrapMessage> PlayerStateOverrides =
            new();

        /// <summary>
        ///     All of the sessions currently subscribed to this UserInterface.
        /// </summary>
        public IReadOnlySet<IPlayerSession> SubscribedSessions => _subscribedSessions;

        [Obsolete("Use system events")]
        public event Action<ServerBoundUserInterfaceMessage>? OnReceiveMessage;

        [Obsolete("Use BoundUIClosedEvent")]
        public event Action<IPlayerSession>? OnClosed;

        public BoundUserInterface(PrototypeData data, ServerUserInterfaceComponent owner)
        {
            RequireInputValidation = data.RequireInputValidation;
            UiKey = data.UiKey;
            Component = owner;
        }

        [Obsolete("Use UserInterfaceSystem")]
        public void SetState(BoundUserInterfaceState state, IPlayerSession? session = null, bool clearOverrides = true)
        {
            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<UserInterfaceSystem>().SetUiState(this, state, session, clearOverrides);
        }

        [Obsolete("Use UserInterfaceSystem")]
        public void Toggle(IPlayerSession session)
        {
            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<UserInterfaceSystem>().ToggleUi(this, session);
        }

        [Obsolete("Use UserInterfaceSystem")]
        public bool Open(IPlayerSession session)
        {
            return IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<UserInterfaceSystem>().OpenUi(this, session);
        }

        [Obsolete("Use UserInterfaceSystem")]
        public bool Close(IPlayerSession session)
        {
            return IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<UserInterfaceSystem>().CloseUi(this, session);
        }

        [Obsolete("Use UserInterfaceSystem")]
        public void CloseAll()
        {
            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<UserInterfaceSystem>().CloseAll(this);
        }

        [Obsolete("Just check SubscribedSessions.Contains")]
        public bool SessionHasOpen(IPlayerSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return _subscribedSessions.Contains(session);
        }

        [Obsolete("Use UserInterfaceSystem")]
        public void SendMessage(BoundUserInterfaceMessage message)
        {
            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<UserInterfaceSystem>().SendUiMessage(this, message);
        }

        [Obsolete("Use UserInterfaceSystem")]
        public void SendMessage(BoundUserInterfaceMessage message, IPlayerSession session)
        {
            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<UserInterfaceSystem>().TrySendUiMessage(this, message, session);
        }

        internal void InvokeOnReceiveMessage(ServerBoundUserInterfaceMessage message)
        {
            OnReceiveMessage?.Invoke(message);
        }

        internal void InvokeOnClosed(IPlayerSession session)
        {
            OnClosed?.Invoke(session);
        }
    }

    [PublicAPI]
    public sealed class ServerBoundUserInterfaceMessage
    {
        public BoundUserInterfaceMessage Message { get; }
        public IPlayerSession Session { get; }

        public ServerBoundUserInterfaceMessage(BoundUserInterfaceMessage message, IPlayerSession session)
        {
            Message = message;
            Session = session;
        }
    }
}

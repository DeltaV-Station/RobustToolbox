using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Robust.Shared.Collections;
using Robust.Shared.Utility;

namespace Robust.Shared.GameObjects
{
    public interface IEventBus : IDirectedEventBus, IBroadcastEventBus
    {
    }

    public interface IDirectedEventBus
    {
        void RaiseLocalEvent<TEvent>(EntityUid uid, TEvent args, bool broadcast = true)
            where TEvent : notnull;

        void RaiseLocalEvent(EntityUid uid, object args, bool broadcast = true);

        void SubscribeLocalEvent<TComp, TEvent>(ComponentEventHandler<TComp, TEvent> handler)
            where TComp : IComponent
            where TEvent : notnull;

        void SubscribeLocalEvent<TComp, TEvent>(
            ComponentEventHandler<TComp, TEvent> handler,
            Type orderType, Type[]? before = null, Type[]? after = null)
            where TComp : IComponent
            where TEvent : notnull;

        #region Ref Subscriptions

        void RaiseLocalEvent<TEvent>(EntityUid uid, ref TEvent args, bool broadcast = true)
            where TEvent : notnull;

        void RaiseLocalEvent(EntityUid uid, ref object args, bool broadcast = true);

        void SubscribeLocalEvent<TComp, TEvent>(ComponentEventRefHandler<TComp, TEvent> handler)
            where TComp : IComponent
            where TEvent : notnull;

        void SubscribeLocalEvent<TComp, TEvent>(
            ComponentEventRefHandler<TComp, TEvent> handler,
            Type orderType, Type[]? before = null, Type[]? after = null)
            where TComp : IComponent
            where TEvent : notnull;

        #endregion

        void UnsubscribeLocalEvent<TComp, TEvent>()
            where TComp : IComponent
            where TEvent : notnull;

        /// <summary>
        /// Dispatches an event directly to a specific component.
        /// </summary>
        /// <remarks>
        /// This has a very specific purpose, and has massive potential to be abused.
        /// DO NOT EXPOSE THIS TO CONTENT.
        /// </remarks>
        /// <typeparam name="TEvent">Event to dispatch.</typeparam>
        /// <param name="component">Component receiving the event.</param>
        /// <param name="args">Event arguments for the event.</param>
        internal void RaiseComponentEvent<TEvent>(IComponent component, TEvent args)
            where TEvent : notnull;

        /// <summary>
        /// Dispatches an event directly to a specific component, by-ref.
        /// </summary>
        /// <remarks>
        /// This has a very specific purpose, and has massive potential to be abused.
        /// DO NOT EXPOSE THIS TO CONTENT.
        /// </remarks>
        /// <typeparam name="TEvent">Event to dispatch.</typeparam>
        /// <param name="component">Component receiving the event.</param>
        /// <param name="args">Event arguments for the event.</param>
        internal void RaiseComponentEvent<TEvent>(IComponent component, ref TEvent args)
            where TEvent : notnull;

        public void OnlyCallOnRobustUnitTestISwearToGodPleaseSomebodyKillThisNightmare();
    }

    internal partial class EntityEventBus : IDisposable
    {
        private const string ValueDispatchError = "Tried to dispatch a value event to a by-reference subscription.";
        private const string RefDispatchError = "Tried to dispatch a ref event to a by-value subscription.";

        private delegate void DirectedEventHandler(EntityUid uid, IComponent comp, ref Unit args);

        private delegate void DirectedEventHandler<TEvent>(EntityUid uid, IComponent comp, ref TEvent args)
            where TEvent : notnull;

        /// <summary>
        /// Constructs a new instance of <see cref="EntityEventBus"/>.
        /// </summary>
        /// <param name="entMan">The entity manager to watch for entity/component events.</param>
        public EntityEventBus(IEntityManager entMan)
        {
            _entMan = entMan;
            _comFac = entMan.ComponentFactory;

            // Dynamic handling of components is only for RobustUnitTest compatibility spaghetti.
            _comFac.ComponentAdded += ETComFacOnComponentAdded;
            _comFac.ComponentReferenceAdded += ETComFacOnComponentReferenceAdded;

            InitEntSubscriptionsArray();
        }

        private void InitEntSubscriptionsArray()
        {
            foreach (var refType in _comFac.GetAllRefTypes())
            {
                CompIdx.AssignArray(ref _entSubscriptions, refType, new Dictionary<Type, DirectedRegistration>());
            }
        }

        /// <inheritdoc />
        void IDirectedEventBus.RaiseComponentEvent<TEvent>(IComponent component, TEvent args)
        {
            ref var unitRef = ref Unsafe.As<TEvent, Unit>(ref args);

            DispatchComponent<TEvent>(component.Owner, component, ref unitRef, false);
        }

        /// <inheritdoc />
        void IDirectedEventBus.RaiseComponentEvent<TEvent>(IComponent component, ref TEvent args)
        {
            ref var unitRef = ref Unsafe.As<TEvent, Unit>(ref args);

            DispatchComponent<TEvent>(component.Owner, component, ref unitRef, true);
        }

        public void OnlyCallOnRobustUnitTestISwearToGodPleaseSomebodyKillThisNightmare()
        {
            IgnoreUnregisteredComponents = true;
        }

        /// <inheritdoc />
        public void RaiseLocalEvent<TEvent>(EntityUid uid, TEvent args, bool broadcast = true)
            where TEvent : notnull
        {
            var type = typeof(TEvent);
            ref var unitRef = ref Unsafe.As<TEvent, Unit>(ref args);

            RaiseLocalEventCore(uid, ref unitRef, type, broadcast, false);
        }

        /// <inheritdoc />
        public void RaiseLocalEvent(EntityUid uid, object args, bool broadcast = true)
        {
            var type = args.GetType();
            ref var unitRef = ref Unsafe.As<object, Unit>(ref args);

            RaiseLocalEventCore(uid, ref unitRef, type, broadcast, false);
        }

        public void RaiseLocalEvent<TEvent>(EntityUid uid, ref TEvent args, bool broadcast = true)
            where TEvent : notnull
        {
            var type = typeof(TEvent);
            ref var unitRef = ref Unsafe.As<TEvent, Unit>(ref args);

            RaiseLocalEventCore(uid, ref unitRef, type, broadcast, true);
        }

        public void RaiseLocalEvent(EntityUid uid, ref object args, bool broadcast = true)
        {
            var type = args.GetType();
            ref var unitRef = ref Unsafe.As<object, Unit>(ref args);

            RaiseLocalEventCore(uid, ref unitRef, type, broadcast, true);
        }

        private void RaiseLocalEventCore(EntityUid uid, ref Unit unitRef, Type type, bool broadcast, bool byRef)
        {
            if (!_eventSubscriptions.TryGetValue(type, out var subs))
                return;

            if (subs.IsOrdered)
            {
                RaiseLocalOrdered(uid, type, subs, ref unitRef, broadcast, byRef);
                return;
            }

            EntDispatch(uid, type, ref unitRef, byRef);

            // we also broadcast it so the call site does not have to.
            if (broadcast)
                ProcessSingleEventCore(EventSource.Local, ref unitRef, subs, byRef);
        }

        /// <inheritdoc />
        public void SubscribeLocalEvent<TComp, TEvent>(ComponentEventHandler<TComp, TEvent> handler)
            where TComp : IComponent
            where TEvent : notnull
        {
            void EventHandler(EntityUid uid, IComponent comp, ref TEvent args)
                => handler(uid, (TComp)comp, args);

            EntSubscribe<TEvent>(
                CompIdx.Index<TComp>(),
                typeof(TComp),
                typeof(TEvent),
                EventHandler,
                null,
                false);
        }

        public void SubscribeLocalEvent<TComp, TEvent>(
            ComponentEventHandler<TComp, TEvent> handler,
            Type orderType,
            Type[]? before = null,
            Type[]? after = null)
            where TComp : IComponent
            where TEvent : notnull
        {
            void EventHandler(EntityUid uid, IComponent comp, ref TEvent args)
                => handler(uid, (TComp)comp, args);

            var orderData = new OrderingData(orderType, before, after);

            EntSubscribe<TEvent>(
                CompIdx.Index<TComp>(),
                typeof(TComp),
                typeof(TEvent),
                EventHandler,
                orderData,
                false);

            RegisterCommon(typeof(TEvent), orderData, out _);
        }

        public void SubscribeLocalEvent<TComp, TEvent>(ComponentEventRefHandler<TComp, TEvent> handler)
            where TComp : IComponent where TEvent : notnull
        {
            void EventHandler(EntityUid uid, IComponent comp, ref TEvent args)
                => handler(uid, (TComp)comp, ref args);

            EntSubscribe<TEvent>(
                CompIdx.Index<TComp>(),
                typeof(TComp),
                typeof(TEvent),
                EventHandler,
                null,
                true);
        }

        public void SubscribeLocalEvent<TComp, TEvent>(ComponentEventRefHandler<TComp, TEvent> handler, Type orderType,
            Type[]? before = null,
            Type[]? after = null) where TComp : IComponent where TEvent : notnull
        {
            void EventHandler(EntityUid uid, IComponent comp, ref TEvent args)
                => handler(uid, (TComp)comp, ref args);

            var orderData = new OrderingData(orderType, before, after);

            EntSubscribe<TEvent>(
                CompIdx.Index<TComp>(),
                typeof(TComp),
                typeof(TEvent),
                EventHandler,
                orderData,
                true);

            RegisterCommon(typeof(TEvent), orderData, out _);
        }

        /// <inheritdoc />
        public void UnsubscribeLocalEvent<TComp, TEvent>()
            where TComp : IComponent
            where TEvent : notnull
        {
            EntUnsubscribe(CompIdx.Index<TComp>(), typeof(TEvent));
        }

        private void ETComFacOnComponentReferenceAdded(ComponentRegistration arg1, CompIdx arg2)
        {
            CompIdx.RefArray(ref _entSubscriptions, arg2) ??= new Dictionary<Type, DirectedRegistration>();
        }

        private void ETComFacOnComponentAdded(ComponentRegistration obj)
        {
            CompIdx.RefArray(ref _entSubscriptions, obj.Idx) ??= new Dictionary<Type, DirectedRegistration>();
        }

        public void OnEntityAdded(EntityUid e)
        {
            EntAddEntity(e);
        }

        public void OnEntityDeleted(EntityUid e)
        {
            EntRemoveEntity(e);
        }

        public void OnComponentAdded(AddedComponentEventArgs e)
        {
            _subscriptionLock = true;

            EntAddComponent(e.BaseArgs.Owner, CompIdx.Index(e.BaseArgs.Component.GetType()));
        }

        public void OnComponentRemoved(RemovedComponentEventArgs e)
        {
            EntRemoveComponent(e.BaseArgs.Owner, CompIdx.Index(e.BaseArgs.Component.GetType()));
        }

        private void EntAddSubscription(
            CompIdx compType,
            Type compTypeObj,
            Type eventType,
            DirectedRegistration registration)
        {
            if (_subscriptionLock)
                throw new InvalidOperationException("Subscription locked.");

            var referenceEvent = eventType.HasCustomAttribute<ByRefEventAttribute>();

            if (referenceEvent != registration.ReferenceEvent)
                throw new InvalidOperationException(
                    $"Attempted to subscribe by-ref and by-value to the same directed event! comp={compTypeObj.Name}, event={eventType.Name} eventIsByRef={referenceEvent} subscriptionIsByRef={registration.ReferenceEvent}");

            if (compType.Value >= _entSubscriptions.Length || _entSubscriptions[compType.Value] is not { } compSubs)
            {
                if (IgnoreUnregisteredComponents)
                    return;

                throw new InvalidOperationException($"Component is not a valid reference type: {compTypeObj.Name}");
            }

            if (compSubs.ContainsKey(eventType))
                throw new InvalidOperationException(
                    $"Duplicate Subscriptions for comp={compTypeObj}, event={eventType.Name}");

            compSubs.Add(eventType, registration);

            var invSubs = _entSubscriptionsInv.GetOrNew(eventType);
            invSubs.Add(compType);

            RegisterCommon(eventType, registration.Ordering, out _);
        }

        private void EntSubscribe<TEvent>(
            CompIdx compType,
            Type compTypeObj,
            Type eventType,
            DirectedEventHandler<TEvent> handler,
            OrderingData? order, bool byReference)
            where TEvent : notnull
        {
            EntAddSubscription(compType, compTypeObj, eventType, new DirectedRegistration(handler, order,
                (EntityUid uid, IComponent comp, ref Unit ev) =>
                {
                    ref var tev = ref Unsafe.As<Unit, TEvent>(ref ev);
                    handler(uid, comp, ref tev);
                }, byReference));
        }

        private void EntUnsubscribe(CompIdx compType, Type eventType)
        {
            if (_subscriptionLock)
                throw new InvalidOperationException("Subscription locked.");

            if (compType.Value >= _entSubscriptions.Length || _entSubscriptions[compType.Value] is not { } compSubs)
            {
                if (IgnoreUnregisteredComponents)
                    return;

                throw new InvalidOperationException("Trying to unsubscribe from unregistered component!");
            }

            var removed = compSubs.Remove(eventType);
            if (removed)
                _entSubscriptionsInv[eventType].Remove(compType);
        }

        private void EntAddEntity(EntityUid euid)
        {
            // odds are at least 1 component will subscribe to an event on the entity, so just
            // preallocate the table now. Dispatch does not need to check this later.
            _entEventTables.Add(euid, new Dictionary<Type, HashSet<CompIdx>>());
        }

        private void EntRemoveEntity(EntityUid euid)
        {
            _entEventTables.Remove(euid);
        }

        private void EntAddComponent(EntityUid euid, CompIdx compType)
        {
            var eventTable = _entEventTables[euid];

            var enumerator = EntGetReferences(compType);
            while (enumerator.MoveNext(out var type))
            {
                var compSubs = _entSubscriptions[type.Value]!;

                foreach (var kvSub in compSubs)
                {
                    if (!eventTable.TryGetValue(kvSub.Key, out var subscribedComps))
                    {
                        subscribedComps = new HashSet<CompIdx>();
                        eventTable.Add(kvSub.Key, subscribedComps);
                    }

                    subscribedComps.Add(type);
                }
            }
        }

        private void EntRemoveComponent(EntityUid euid, CompIdx compType)
        {
            var eventTable = _entEventTables[euid];

            var enumerator = EntGetReferences(compType);
            while (enumerator.MoveNext(out var type))
            {
                var compSubs = _entSubscriptions[type.Value]!;

                foreach (var kvSub in compSubs)
                {
                    if (!eventTable.TryGetValue(kvSub.Key, out var subscribedComps))
                        return;

                    subscribedComps.Remove(type);
                }
            }
        }

        private void EntDispatch(EntityUid euid, Type eventType, ref Unit args, bool dispatchByReference)
        {
            if (!EntTryGetSubscriptions(eventType, euid, out var enumerator))
                return;

            while (enumerator.MoveNext(out var tuple))
            {
                var (component, reg) = tuple.Value;
                if (reg.ReferenceEvent != dispatchByReference)
                    ThrowByRefMisMatch();

                reg.Handler(euid, component, ref args);
            }
        }

        private void EntCollectOrdered(
            EntityUid euid,
            Type eventType,
            ref ValueList<OrderedEventDispatch> found,
            bool byRef)
        {
            var eventTable = _entEventTables[euid];

            if (!eventTable.TryGetValue(eventType, out var subscribedComps))
                return;

            foreach (var compType in subscribedComps)
            {
                var compSubs = _entSubscriptions[compType.Value]!;

                if (!compSubs.TryGetValue(eventType, out var reg))
                    return;

                if (reg.ReferenceEvent != byRef)
                    ThrowByRefMisMatch();

                var component = _entMan.GetComponent(euid, compType);

                found.Add(new OrderedEventDispatch((ref Unit ev) => reg.Handler(euid, component, ref ev), reg.Order));
            }
        }

        private void DispatchComponent<TEvent>(EntityUid euid, IComponent component, ref Unit args,
            bool dispatchByReference)
            where TEvent : notnull
        {
            var enumerator = EntGetReferences(CompIdx.Index(component.GetType()));
            while (enumerator.MoveNext(out var type))
            {
                var compSubs = _entSubscriptions[type.Value]!;

                if (!compSubs.TryGetValue(typeof(TEvent), out var reg))
                    continue;

                if (reg.ReferenceEvent != dispatchByReference)
                    ThrowByRefMisMatch();

                reg.Handler(euid, component, ref args);
            }
        }

        /// <summary>
        ///     Enumerates the type's component references, returning the type itself last.
        /// </summary>
        private ReferencesEnumerator EntGetReferences(CompIdx type)
        {
            return new(type, _comFac.GetRegistration(type).References);
        }

        /// <summary>
        ///     Enumerates all subscriptions for an event on a specific entity, returning the component instances and registrations.
        /// </summary>
        private bool EntTryGetSubscriptions(Type eventType, EntityUid euid, out SubscriptionsEnumerator enumerator)
        {
            if (!_entEventTables.TryGetValue(euid, out var eventTable))
            {
                enumerator = default!;
                return false;
            }

            // No subscriptions to this event type, return null.
            if (!eventTable.TryGetValue(eventType, out var subscribedComps))
            {
                enumerator = default;
                return false;
            }

            enumerator = new(eventType, subscribedComps.GetEnumerator(), _entSubscriptions, euid, _entMan);
            return true;
        }

        private void EntClear()
        {
            _entEventTables = new();
            _subscriptionLock = false;
        }

        public void ClearEventTables()
        {
            EntClear();

            foreach (var sub in _entSubscriptions)
            {
                sub?.Clear();
            }
        }

        public void Dispose()
        {
            _comFac.ComponentAdded -= ETComFacOnComponentAdded;
            _comFac.ComponentReferenceAdded -= ETComFacOnComponentReferenceAdded;

            // punishment for use-after-free
            _entMan = null!;
            _comFac = null!;
            _entEventTables = null!;
            _entSubscriptions = null!;
            _entSubscriptionsInv = null!;
        }

        private struct ReferencesEnumerator
        {
            private readonly CompIdx _baseType;
            private readonly ValueList<CompIdx> _list;
            private readonly int _totalLength;
            private int _idx;

            public ReferencesEnumerator(CompIdx baseType, ValueList<CompIdx> list)
            {
                _baseType = baseType;
                _list = list;
                _totalLength = list.Count;
                _idx = 0;
            }

            public bool MoveNext(out CompIdx type)
            {
                if (_idx >= _totalLength)
                {
                    if (_idx++ == _totalLength)
                    {
                        type = _baseType;
                        return true;
                    }

                    type = default;
                    return false;
                }

                type = _list[_idx++];
                if (type == _baseType)
                    return MoveNext(out type);

                return true;
            }
        }

        private struct SubscriptionsEnumerator : IDisposable
        {
            private readonly Type _eventType;
            private HashSet<CompIdx>.Enumerator _enumerator;
            private readonly Dictionary<Type, DirectedRegistration>?[] _subscriptions;
            private readonly EntityUid _uid;
            private readonly IEntityManager _entityManager;

            public SubscriptionsEnumerator(
                Type eventType,
                HashSet<CompIdx>.Enumerator enumerator,
                Dictionary<Type, DirectedRegistration>?[] subscriptions,
                EntityUid uid,
                IEntityManager entityManager)
            {
                _eventType = eventType;
                _enumerator = enumerator;
                _subscriptions = subscriptions;
                _entityManager = entityManager;
                _uid = uid;
            }

            public bool MoveNext(
                [NotNullWhen(true)] out (IComponent Component, DirectedRegistration Registration)? tuple)
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (!_enumerator.MoveNext())
                {
                    tuple = null;
                    return false;
                }

                var compType = _enumerator.Current;
                var compSubs = _subscriptions[compType.Value]!;

                if (!compSubs.TryGetValue(_eventType, out var registration))
                {
                    tuple = null;
                    return false;
                }

                tuple = (_entityManager.GetComponent(_uid, compType), registration);
                return true;
            }

            public void Dispose()
            {
                _enumerator.Dispose();
            }
        }

        private sealed class DirectedRegistration : OrderedRegistration
        {
            public readonly Delegate Original;
            public readonly DirectedEventHandler Handler;
            public readonly bool ReferenceEvent;

            public DirectedRegistration(
                Delegate original,
                OrderingData? ordering,
                DirectedEventHandler handler,
                bool referenceEvent) : base(ordering)
            {
                Original = original;
                Handler = handler;
                ReferenceEvent = referenceEvent;
            }

            public void SetOrder(int order)
            {
                Order = order;
            }
        }
    }

    public delegate void ComponentEventHandler<in TComp, in TEvent>(EntityUid uid, TComp component, TEvent args)
        where TComp : IComponent
        where TEvent : notnull;

    public delegate void ComponentEventRefHandler<in TComp, TEvent>(EntityUid uid, TComp component, ref TEvent args)
        where TComp : IComponent
        where TEvent : notnull;
}

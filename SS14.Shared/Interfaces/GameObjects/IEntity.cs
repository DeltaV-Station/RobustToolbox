using Lidgren.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SS14.Shared.GameObjects;
using YamlDotNet.RepresentationModel;

namespace SS14.Shared.Interfaces.GameObjects
{
    public interface IEntity
    {
        IEntityNetworkManager EntityNetworkManager { get; }
        IEntityManager EntityManager { get; }

        string Name { get; set; }
        int Uid { get; set; }

        bool Initialized { get; set; }

        EntityPrototype Prototype { get; set; }
        event EntityShutdownEvent OnShutdown;

        /// <summary>
        /// Called after the entity is construted by its prototype to load parameters
        /// from the prototype's <c>data</c> field.
        /// </summary>
        /// <remarks>
        /// This method does not get called in case no data field is provided.
        /// </remarks>
        /// <param name="parameters">The mapping representing the <c>data</c> field.</param>
        void LoadData(YamlMappingNode parameters);

        /// <summary>
        /// Match
        ///
        /// Allows us to fetch entities with a defined SET of components
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        bool Match(IEntityQuery query);

        /// <summary>
        /// Public method to add a component to an entity.
        /// Calls the component's onAdd method, which also adds it to the component manager.
        /// </summary>
        /// <param name="component">The component.</param>
        void AddComponent(IComponent component);

        /// <summary>
        /// Public method to remove a component from an entity.
        /// Calls the onRemove method of the component, which handles removing it
        /// from the component manager and shutting down the component.
        /// </summary>
        /// <param name="family"></param>
        void RemoveComponent(IComponent component);

        void RemoveComponent<T>() where T : IComponent;

        bool HasComponent<T>() where T : IComponent;
        bool HasComponent(Type t);
        T GetComponent<T>() where T : IComponent;
        IComponent GetComponent(Type type);
        IComponent GetComponent(uint netID);
        bool TryGetComponent<T>(out T component) where T : class, IComponent;
        bool TryGetComponent(Type type, out IComponent component);
        bool TryGetComponent(uint netID, out IComponent component);

        void Shutdown();
        IEnumerable<IComponent> GetComponents();
        IEnumerable<T> GetComponents<T>() where T : IComponent;
        void SendMessage(object sender, ComponentMessageType type, params object[] args);

        /// <summary>
        /// Allows components to send messages
        /// </summary>
        /// <param name="sender">the component doing the sending</param>
        /// <param name="type">the type of message</param>
        /// <param name="args">message parameters</param>
        void SendMessage(object sender, ComponentMessageType type, List<ComponentReplyMessage> replies,
                         params object[] args);

        /// <summary>
        /// Requests Description string from components and returns it. If no component answers, returns default description from template.
        /// </summary>
        string GetDescriptionString(); //This needs to go here since it can not be bound to any single component.

        /// <summary>
        /// Sends a message to the counterpart component on the server side
        /// </summary>
        /// <param name="component">Sending component</param>
        /// <param name="method">Net Delivery Method</param>
        /// <param name="messageParams">Parameters</param>
        void SendComponentNetworkMessage(IComponent component, NetDeliveryMethod method, params object[] messageParams);

        /// <summary>
        /// Sends a message to the counterpart component on the server side
        /// </summary>
        /// <param name="component">Sending component</param>
        /// <param name="method">Net Delivery Method</param>
        /// <param name="recipient">The intended recipient netconnection (if null send to all)</param>
        /// <param name="messageParams">Parameters</param>
        void SendDirectedComponentNetworkMessage(IComponent component, NetDeliveryMethod method,
                                                 NetConnection recipient, params object[] messageParams);

        /// <summary>
        /// Sets up variables and shite
        /// </summary>
        void Initialize();

        void HandleNetworkMessage(IncomingEntityMessage message);

        /// <summary>
        /// Client method to handle an entity state object
        /// </summary>
        /// <param name="state"></param>
        void HandleEntityState(EntityState state);

        /// <summary>
        /// Serverside method to prepare an entity state object
        /// </summary>
        /// <returns></returns>
        EntityState GetEntityState();

        void SubscribeEvent<T>(EntityEventHandler<EntityEventArgs> evh, IEntityEventSubscriber s) where T : EntityEventArgs;
        void UnsubscribeEvent<T>(IEntityEventSubscriber s) where T : EntityEventArgs;
        void RaiseEvent(EntityEventArgs toRaise);
    }
}

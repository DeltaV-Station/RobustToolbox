using System;
using System.Collections.Generic;
using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;

namespace Robust.Shared.Serialization.TypeSerializers
{
    [TypeSerializer]
    public class ComponentRegistrySerializer : ITypeSerializer<EntityPrototype.ComponentRegistry>
    {
        [Dependency] private readonly IServ3Manager _serv3Manager = default!;
        [Dependency] private readonly IComponentFactory _componentFactory = default!;

        public EntityPrototype.ComponentRegistry NodeToType(IDataNode node, ISerializationContext? context = null)
        {
            if (node is not ISequenceDataNode componentsequence) return new EntityPrototype.ComponentRegistry();
            var components = new EntityPrototype.ComponentRegistry();
            foreach (var componentMapping in componentsequence.Sequence.Cast<IMappingDataNode>())
            {
                string compType = ((IValueDataNode)componentMapping.GetNode("type")).GetValue();
                // See if type exists to detect errors.
                switch (_componentFactory.GetComponentAvailability(compType))
                {
                    case ComponentAvailability.Available:
                        break;

                    case ComponentAvailability.Ignore:
                        continue;

                    case ComponentAvailability.Unknown:
                        Logger.Error($"Unknown component '{compType}' in prototype!");
                        continue;
                }

                // Has this type already been added?
                if (components.Keys.Contains(compType))
                {
                    Logger.Error($"Component of type '{compType}' defined twice in prototype!");
                    continue;
                }

                var copy = (componentMapping.Copy() as IMappingDataNode)!;
                copy.RemoveNode("type");

                var dataClassType = _serv3Manager.GetDataClassType(_componentFactory.GetRegistration(compType).Type);
                var data = (DataClass)_serv3Manager.ReadValue(dataClassType, copy);

                components[compType] = data;
            }

            var referenceTypes = new List<Type>();
            // Assert that there are no conflicting component references.
            foreach (var componentName in components.Keys)
            {
                var registration = _componentFactory.GetRegistration(componentName);
                foreach (var compType in registration.References)
                {
                    if (referenceTypes.Contains(compType))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate component reference in prototype: '{compType}'");
                    }

                    referenceTypes.Add(compType);
                }
            }

            return components;
        }

        public IDataNode TypeToNode(EntityPrototype.ComponentRegistry value, IDataNodeFactory nodeFactory,
            bool alwaysWrite = false,
            ISerializationContext? context = null)
        {
            var compSequence = nodeFactory.GetSequenceNode();
            foreach (var (type, component) in value)
            {
                var node = _serv3Manager.WriteValue(component.GetType(), component, nodeFactory, alwaysWrite, context);
                if (node is not IMappingDataNode mapping) throw new InvalidNodeTypeException();
                if (mapping.Children.Count != 0)
                {
                    mapping.AddNode("type", nodeFactory.GetValueNode(type));
                    compSequence.Add(mapping);
                }
            }

            return compSequence;
        }
    }
}

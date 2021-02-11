using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Interfaces.GameObjects;
using Robust.Shared.Interfaces.Reflection;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes.DataClasses;
using Robust.Shared.Prototypes.DataClasses.Attributes;
using Robust.Shared.Serialization.Manager;

namespace Robust.Shared.Prototypes
{
    public class DataClassManager : IDataClassManager
    {
        [Dependency] private readonly IComponentFactory _componentFactory = default!;
        [Dependency] private readonly IReflectionManager _reflectionManager = default!;
        [Dependency] private readonly ISerializationManager _serializationManager = default!;

        private Dictionary<Type, DataClassLink> _dataClassLinks = new();

        public void Initialize()
        {
            foreach (var baseType in _reflectionManager.FindTypesWithAttribute<ImplicitDataClassForInheritorsAttribute>())
            {
                foreach (var child in _reflectionManager.GetAllChildren(baseType))
                {
                    if(_dataClassLinks.ContainsKey(child)) continue;
                    var dataClassLink = new DataClassLink(child, ResolveDataClass(child));
                    _dataClassLinks.Add(child, dataClassLink);
                }
            }

            foreach (var type in _reflectionManager.FindTypesWithAttribute<DataClassAttribute>())
            {
                if(_dataClassLinks.ContainsKey(type)) continue;
                var dataClassLink = new DataClassLink(type, ResolveDataClass(type));
                _dataClassLinks.Add(type, dataClassLink);
            }
        }

        private Type ResolveDataClass(Type type)
        {
            Type dataClassType;
            var attr = (DataClassAttribute?)Attribute.GetCustomAttribute(type, typeof(DataClassAttribute));
            if (attr != null && attr.ClassName != null)
            {
                dataClassType = attr.ClassName;
            }
            else
            {
                var nonGenericType = type;
                if (nonGenericType.IsGenericType)
                {
                    nonGenericType = nonGenericType.GetGenericTypeDefinition();
                }

                var classType = _reflectionManager.GetType($"{nonGenericType.Namespace}.{nonGenericType.Name}_AUTODATA");
                if (classType == null)
                {
                    if (nonGenericType.BaseType == null)
                        throw new InvalidProgramException($"No Dataclass found for type {nonGenericType}");
                    dataClassType = ResolveDataClass(nonGenericType.BaseType);
                }
                else
                {
                    dataClassType = classType;
                }
            }

            return dataClassType;
        }

        public DataClass GetEmptyDataClass(Type classType)
        {
            return (DataClass) Activator.CreateInstance(GetDataClassType(classType))!;
        }

        public Type GetDataClassType(Type classType)
        {
            return GetDataClassLink(classType).DataClassType;
        }

        public Type GetComponentDataClassType(string compName)
        {
            return GetDataClassType(_componentFactory.GetRegistration(compName).Type);
        }

        private DataClassLink GetDataClassLink(Type type)
        {
            if (!_dataClassLinks.TryGetValue(type, out var dataClassLink))
            {
                throw new ArgumentException("No dataclasslink registered!", nameof(type));
            }

            return dataClassLink;
        }

        public DataClass GetEmptyComponentDataClass(string compName)
        {
            return GetEmptyDataClass(_componentFactory.GetRegistration(compName).Type);
        }

        public bool TryGetDataClassField<T>(DataClass dataClass, string name, [NotNullWhen(true)] out T? value)
        {
            foreach (var classLink in _dataClassLinks)
            {
                if (classLink.Value.DataClassType == dataClass.GetType())
                {
                    value = (T?) classLink.Value.GetFieldDelegate(dataClass, name);
                    return value != null;
                }
            }

            value = default;
            return false;
        }

        public void PopulateObject(object obj, DataClass dataClass)
        {
            var link = GetDataClassLink(obj.GetType());
            if (!link.DataClassType.IsInstanceOfType(dataClass))
                throw new ArgumentException("Invalid Dataclass supplied in PopulateObject!", nameof(dataClass));

            link.PopulateObjectDelegate(obj, dataClass, _serializationManager);
        }

        public void PopulateDataClass(object obj, DataClass dataClass)
        {
            var link = GetDataClassLink(obj.GetType());
            if (!link.DataClassType.IsInstanceOfType(dataClass))
                throw new ArgumentException("Invalid Dataclass supplied in PopulateObject!", nameof(dataClass));

            link.PopulateDataclassDelegate(obj, dataClass, _serializationManager);
        }
    }
}

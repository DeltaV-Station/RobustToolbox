using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Interfaces.Reflection;
using Robust.Shared.Interfaces.Serialization;
using Robust.Shared.IoC;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Robust.Shared.Serialization.Manager
{
    public class SerializationManager : ISerializationManager
    {
        [Dependency] private readonly IReflectionManager _reflectionManager = default!;

        private Dictionary<Type, SerializationDataDefinition> _dataDefinitions = new();

        public void Initialize()
        {
            //generating all datadefinitions except pure exposedata inheritors
            foreach (var meansAttr in _reflectionManager.FindTypesWithAttribute<MeansYamlDefinition>())
            {
                foreach (var type in _reflectionManager.FindTypesWithAttribute(meansAttr))
                {
                    _dataDefinitions.Add(type, new SerializationDataDefinition(type));
                }
            }
        }

        public SerializationDataDefinition? GetDataDefinition(Type type)
        {
            if (_dataDefinitions.TryGetValue(type, out var dataDefinition)) return dataDefinition;

            if (!typeof(IExposeData).IsAssignableFrom(type)) return null;

            dataDefinition = new SerializationDataDefinition(type);
            _dataDefinitions.Add(type, dataDefinition);

            return dataDefinition;
        }

        public bool TryGetDataDefinition(Type type, [NotNullWhen(true)] out SerializationDataDefinition? dataDefinition)
        {
            dataDefinition = GetDataDefinition(type);
            return dataDefinition != null;
        }

        public object Populate(Type type, YamlObjectSerializer serializer)
        {
            if (!serializer.Reading) throw new InvalidOperationException();

            var currentType = type;
            var dataDef = GetDataDefinition(type);
            if (dataDef == null) return Activator.CreateInstance(type)!;

            var obj = Activator.CreateInstance(dataDef.Type)!;

            while (currentType != null)
            {
                dataDef = GetDataDefinition(currentType);
                if(dataDef != null)
                    obj = dataDef.InvokePopulateDelegate(obj, serializer, this);

                currentType = currentType.BaseType;
            }

            return obj;
        }

        public void Serialize(Type type, object obj, YamlObjectSerializer serializer, bool alwaysWrite = false)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));

            var currentType = type;

            while (currentType != null)
            {
                var dataDef = GetDataDefinition(type);
                if (dataDef?.CanCallWith(obj) != true)
                    throw new ArgumentException($"Supplied parameter does not fit with datadefinition of {type}.", nameof(obj));
                dataDef?.InvokeSerializeDelegate(obj, serializer, this, alwaysWrite);
                currentType = currentType.BaseType;
            }
        }

        public object PushInheritance(object source, object target)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var commonType = TypeHelpers.FindCommonType(source.GetType(), target.GetType());
            if(commonType == null)
            {
                throw new InvalidOperationException("Could not find common type in PushInheritance!");
            }

            while (commonType != null)
            {
                var dataDef = GetDataDefinition(commonType);
                if(dataDef != null)
                    target = dataDef.InvokePushInheritanceDelegate(source, target, this);
                commonType = commonType.BaseType;
            }

            return target;
        }

        public object Copy(object source, object target)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));

            var commonType = TypeHelpers.FindCommonType(source.GetType(), target.GetType());
            if(commonType == null)
            {
                throw new InvalidOperationException("Could not find common type in PushInheritance!");
            }

            while (commonType != null)
            {
                var dataDef = GetDataDefinition(commonType);
                if(dataDef != null)
                    target = dataDef.CopyDelegate(source, target, this);
                commonType = commonType.BaseType;
            }

            return target;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using JetBrains.Annotations;
using Robust.Shared.Interfaces.Reflection;
using Robust.Shared.Interfaces.Serialization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;

namespace Robust.Shared.Serialization.Manager
{
    public class SerializationDataDefinition
    {
        public readonly Type Type;

        public IReadOnlyList<BaseFieldDefinition> FieldDefinitions => _baseFieldDefinitions;

        private readonly List<BaseFieldDefinition> _baseFieldDefinitions = new();

        public readonly Action<object, YamlObjectSerializer> PopulateDelegate;

        public readonly Action<object, YamlObjectSerializer> SerializeDelegate;

        public bool CanCallWith(object obj) => Type.IsInstanceOfType(obj);

        public SerializationDataDefinition(Type type, IReflectionManager _reflectionManager)
        {
            Type = type;

            foreach (var field in type.GetAllFields())
            {
                if(field.DeclaringType != type) continue;
                var attr = (YamlFieldAttribute?)Attribute.GetCustomAttribute(field, typeof(YamlFieldAttribute));
                if(attr == null) continue;
                _baseFieldDefinitions.Add(new FieldDefinition(attr, field));
            }

            foreach (var property in type.GetAllProperties())
            {
                if(property.DeclaringType != type) continue;
                var attr = (YamlFieldAttribute?)Attribute.GetCustomAttribute(property, typeof(YamlFieldAttribute));
                if(attr == null) continue;
                _baseFieldDefinitions.Add(new PropertyDefinition(attr, property));
            }

            PopulateDelegate = EmitPopulateDelegate();
            SerializeDelegate = EmitSerializeDelegate();
        }

        private Action<object, YamlObjectSerializer> EmitPopulateDelegate()
        {
            var dynamicMethod = new DynamicMethod(
                $"_populateDelegate<>{Type}",
                typeof(void),
                new[] {typeof(object), typeof(YamlObjectSerializer)},
                Type,
                true);
            dynamicMethod.DefineParameter(1, ParameterAttributes.In, "obj");
            dynamicMethod.DefineParameter(2, ParameterAttributes.In, "serializer");
            var generator = dynamicMethod.GetILGenerator();

            if (typeof(IExposeData).IsAssignableFrom(Type))
            {
                generator.EmitExposeDataCall();
            }

            foreach (var fieldDefinition in _baseFieldDefinitions)
            {
                generator.EmitPopulateField(fieldDefinition);
            }

            generator.Emit(OpCodes.Ret);

            return dynamicMethod.CreateDelegate<Action<object, YamlObjectSerializer>>();
        }

        private Action<object, YamlObjectSerializer> EmitSerializeDelegate()
        {
            var dynamicMethod = new DynamicMethod(
                $"_serializeDelegate<>{Type}",
                typeof(void),
                new[] {typeof(object), typeof(YamlObjectSerializer)},
                Type,
                true);
            dynamicMethod.DefineParameter(1, ParameterAttributes.In, "obj");
            dynamicMethod.DefineParameter(2, ParameterAttributes.In, "serializer");
            var generator = dynamicMethod.GetILGenerator();

            if (typeof(IExposeData).IsAssignableFrom(Type))
            {
                generator.EmitExposeDataCall();
            }

            foreach (var fieldDefinition in _baseFieldDefinitions)
            {
                generator.EmitSerializeField(fieldDefinition);
            }

            generator.Emit(OpCodes.Ret);

            return dynamicMethod.CreateDelegate<Action<object, YamlObjectSerializer>>();
        }

        public abstract class BaseFieldDefinition
        {
            public readonly YamlFieldAttribute Attribute;

            protected BaseFieldDefinition(YamlFieldAttribute attr)
            {
                Attribute = attr;
            }

            public abstract Type FieldType { get; }
        }

        public class FieldDefinition : BaseFieldDefinition
        {
            public readonly FieldInfo FieldInfo;
            public override Type FieldType => FieldInfo.FieldType;


            public FieldDefinition(YamlFieldAttribute attr, FieldInfo fieldInfo) : base(attr)
            {
                FieldInfo = fieldInfo;
            }
        }

        public class PropertyDefinition : BaseFieldDefinition
        {
            public readonly PropertyInfo PropertyInfo;
            public override Type FieldType => PropertyInfo.PropertyType;

            public PropertyDefinition(YamlFieldAttribute attr, PropertyInfo propertyInfo) : base(attr)
            {
                PropertyInfo = propertyInfo;
            }
        }
    }
}

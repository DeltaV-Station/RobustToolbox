using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Robust.Shared.Log;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;

namespace Robust.Shared.Prototypes.DataClasses
{
    public class DataClassLink
    {
        public readonly Type Type;
        public readonly Type DataClassType;

        private List<LinkEntry> _actualFields = new();
        private List<LinkEntry> _dataclassFields = new();

        public readonly Action<object, DataClass, ISerializationManager> PopulateObjectDelegate;
        public readonly Action<object, DataClass, ISerializationManager> PopulateDataclassDelegate;
        public readonly Func<DataClass, string, object> GetFieldDelegate;

        public DataClassLink(Type type, Type dataClassType)
        {
            Type = type;
            DataClassType = dataClassType;

            foreach (var abstractFieldInfo in type.GetAllPropertiesAndFields())
            {
                var attr = abstractFieldInfo.GetCustomAttribute<BaseDataFieldAttribute>();
                if(attr == null) continue;

                _actualFields.Add(new LinkEntry(abstractFieldInfo, attr));
            }

            foreach (var abstractFieldInfo in dataClassType.GetAllPropertiesAndFields())
            {
                var attr = abstractFieldInfo.GetCustomAttribute<BaseDataFieldAttribute>();
                if(attr == null) continue;

                _dataclassFields.Add(new LinkEntry(abstractFieldInfo, attr));
            }

            PopulateObjectDelegate = EmitPopulateObjectDelegate();
            PopulateDataclassDelegate = EmitPopulateDataclassDelegate();
            GetFieldDelegate = EmitGetFieldDelegate();
        }

        private Func<DataClass, string, object?> EmitGetFieldDelegate()
        {
            var dynamicMethod = new DynamicMethod(
                $"_populateObjectFromDC<>{Type}<>{DataClassType}",
                typeof(object),
                new[] {typeof(DataClass), typeof(string)},
                Type,
                true);
            dynamicMethod.DefineParameter(1, ParameterAttributes.In, "dataClass");
            dynamicMethod.DefineParameter(2, ParameterAttributes.In, "name");
            dynamicMethod.DefineParameter(2, ParameterAttributes.Out, "value");
            var generator = dynamicMethod.GetILGenerator();

            foreach (var dataclassField in _dataclassFields)
            {
                var notIt = generator.DefineLabel();
                generator.Emit(OpCodes.Ldarg_1);
                generator.Emit(OpCodes.Ldstr, dataclassField.DataFieldAttribute.Tag);
                generator.Emit(OpCodes.Brfalse_S, notIt);

                generator.Emit(OpCodes.Ldarg_0);
                generator.EmitLdfld(dataclassField.FieldInfo);
                generator.Emit(OpCodes.Ret);

                generator.MarkLabel(notIt);
            }

            generator.Emit(OpCodes.Ldnull);
            generator.Emit(OpCodes.Ret);

            return dynamicMethod.CreateDelegate<Func<DataClass, string, object?>>();
        }

        private Action<object, DataClass, ISerializationManager> EmitPopulateObjectDelegate()
        {
            var dynamicMethod = new DynamicMethod(
                $"_populateObjectFromDC<>{Type}<>{DataClassType}",
                typeof(void),
                new[] {typeof(object), typeof(DataClass), typeof(ISerializationManager)},
                Type,
                true);
            dynamicMethod.DefineParameter(1, ParameterAttributes.In, "obj");
            dynamicMethod.DefineParameter(2, ParameterAttributes.In, "dataclass");
            dynamicMethod.DefineParameter(3, ParameterAttributes.In, "serv3Mgr");
            var generator = dynamicMethod.GetILGenerator();

            foreach (var actualField in _actualFields)
            {
                LinkEntry? counterPart = null;
                foreach (var dataclassField in _dataclassFields)
                {
                    if (dataclassField.DataFieldAttribute.Tag == actualField.DataFieldAttribute.Tag)
                    {
                        counterPart = dataclassField;
                        break;
                    }
                }

                if (counterPart == null)
                    throw new InvalidOperationException(
                        "Could not find field-counterpart while generating PopulateObjectDelegate!");

                generator.EmitCopy(1, counterPart.FieldInfo, 0, actualField.FieldInfo, 2, true);
            }

            generator.Emit(OpCodes.Ret);

            return dynamicMethod.CreateDelegate<Action<object, DataClass, ISerializationManager>>();
        }

        private Action<object, DataClass, ISerializationManager> EmitPopulateDataclassDelegate()
        {
            var dynamicMethod = new DynamicMethod(
                $"_populateDCFromObject<>{Type}<>{DataClassType}",
                typeof(void),
                new[] {typeof(object), typeof(DataClass), typeof(ISerializationManager)},
                Type,
                true);
            dynamicMethod.DefineParameter(1, ParameterAttributes.In, "obj");
            dynamicMethod.DefineParameter(2, ParameterAttributes.In, "dataclass");
            dynamicMethod.DefineParameter(3, ParameterAttributes.In, "serv3Mgr");
            var generator = dynamicMethod.GetILGenerator();

            foreach (var actualField in _actualFields)
            {
                LinkEntry? counterPart = null;
                foreach (var dataclassField in _dataclassFields)
                {
                    if (dataclassField.DataFieldAttribute.Tag == actualField.DataFieldAttribute.Tag)
                    {
                        counterPart = dataclassField;
                        break;
                    }
                }

                if (counterPart == null)
                    throw new InvalidOperationException(
                        "Could not find field-counterpart while generating PopulateDataclassDelegate!");

                generator.EmitCopy(0, actualField.FieldInfo, 1, counterPart.FieldInfo, 2);
            }

            generator.Emit(OpCodes.Ret);

            return dynamicMethod.CreateDelegate<Action<object, DataClass, ISerializationManager>>();
        }

        private class LinkEntry
        {
            public readonly AbstractFieldInfo FieldInfo;
            public readonly BaseDataFieldAttribute DataFieldAttribute;

            public LinkEntry(AbstractFieldInfo fieldInfo, BaseDataFieldAttribute dataFieldAttribute)
            {
                FieldInfo = fieldInfo;
                if (fieldInfo is SpecificPropertyInfo propertyInfo && (propertyInfo.PropertyInfo.GetMethod == null || propertyInfo.PropertyInfo.SetMethod == null))
                {
                    throw new InvalidOperationException(
                        "Property without getter or setter was annotated with YamlfieldAttribute");
                }
                DataFieldAttribute = dataFieldAttribute;
            }
        }
    }
}

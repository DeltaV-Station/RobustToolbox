using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Robust.Shared.Interfaces.Serialization;
using Robust.Shared.Utility;

namespace Robust.Shared.Serialization.Manager
{
    public static class SerializationILExtensions
    {
        private static bool IsReadDataField(MethodInfo method)
        {
            return method.Name == nameof(ObjectSerializer.ReadDataField) && method.GetParameters().Length == 2;
        }


        /// <summary>
        /// WARNING: This method assumes the object is at position 0 & the yamlobjectserializer is at position 1
        /// </summary>
        public static void EmitPopulateField(this ILGenerator generator,
            SerializationDataDefinition.FieldDefinition fieldDefinition, int localIdx, int defaultValueIdx)
        {
            if (fieldDefinition.FieldType.IsPrimitive || fieldDefinition.FieldType == typeof(string))
            {
                //load serializer
                generator.Emit(OpCodes.Ldarg_1);

                // load the yaml tag
                generator.Emit(OpCodes.Ldstr, fieldDefinition.Attribute.Tag);

                //load the default value
                generator.Emit(OpCodes.Ldarg_3);
                generator.Emit(OpCodes.Ldc_I4, defaultValueIdx);
                generator.Emit(OpCodes.Ldelem, fieldDefinition.FieldType);

                //reading datafield using the objectserializer
                var readDataFieldMethod =
                    typeof(ObjectSerializer).GetMethods().First(IsReadDataField)?
                        .MakeGenericMethod(fieldDefinition.FieldType);
                Debug.Assert(readDataFieldMethod != null, nameof(readDataFieldMethod) + " != null");
                generator.Emit(OpCodes.Callvirt, readDataFieldMethod);
            }
            else
            {
                //load serv3manager
                generator.Emit(OpCodes.Ldarg_2);

                //load type
                generator.Emit(OpCodes.Ldtoken, fieldDefinition.FieldType);
                generator.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);

                //todo shouldn't this be a new serializer that has the subnode?
                //load serializer
                generator.Emit(OpCodes.Ldarg_1);

                generator.Emit(OpCodes.Ldstr, fieldDefinition.Attribute.Tag);
                var readforkmethod = typeof(ObjectSerializer).GetMethod(nameof(ObjectSerializer.ReadDataFieldFork))?
                    .MakeGenericMethod(fieldDefinition.FieldType);
                Debug.Assert(readforkmethod != null, nameof(readforkmethod) + " != null");
                generator.Emit(OpCodes.Call, readforkmethod);

                //call populate(type, serializer)
                var populateMethod = typeof(ISerializationManager).GetMethod(nameof(ISerializationManager.Populate));
                Debug.Assert(populateMethod != null, nameof(populateMethod) + " != null");
                generator.Emit(OpCodes.Callvirt, populateMethod);
            }

            //storing the return value so we can populate it into our field later
            generator.Emit(OpCodes.Stloc, localIdx);

            //loading back our value
            generator.Emit(OpCodes.Ldloc, localIdx);

            //loading default value
            generator.Emit(OpCodes.Ldarg_3);
            generator.Emit(OpCodes.Ldc_I4, defaultValueIdx);
            generator.Emit(OpCodes.Ldelem, fieldDefinition.FieldType);


            //checking if the value is default. if so, we skip setting the field
            var isDefaultLabel = generator.DefineLabel();
            generator.EmitEquals(fieldDefinition.FieldType, isDefaultLabel);

            //load object
            generator.Emit(OpCodes.Ldarg_0);
            //load value
            generator.Emit(OpCodes.Ldloc, localIdx);
            //setting the field
            generator.EmitStfld(fieldDefinition.FieldInfo);

            generator.MarkLabel(isDefaultLabel);
        }

        public static void EmitPushInheritanceField(this ILGenerator generator,
            SerializationDataDefinition.FieldDefinition fieldDefinition, int defaultValueIdx)
        {
            var isDefaultValue = generator.DefineLabel();

            generator.Emit(OpCodes.Ldarg_0);
            generator.EmitLdfld(fieldDefinition.FieldInfo);
            generator.Emit(OpCodes.Ldarg_3);
            generator.Emit(OpCodes.Ldc_I4, defaultValueIdx);
            generator.Emit(OpCodes.Ldelem, fieldDefinition.FieldType);
            generator.EmitEquals(fieldDefinition.FieldType, isDefaultValue);

            generator.EmitCopy(0, fieldDefinition.FieldInfo, 1, fieldDefinition.FieldInfo, 2);

            generator.MarkLabel(isDefaultValue);
        }

        public static void EmitCopy(this ILGenerator generator, int fromArg, AbstractFieldInfo fromField, int toArg,
            AbstractFieldInfo toField, int mgrArg, bool assumeValueNotNull = false)
        {
            if (assumeValueNotNull)
            {
                var type = fromField.FieldType;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    type = type.GenericTypeArguments.First();
                }
                if (!toField.FieldType.IsAssignableFrom(type))
                    throw new InvalidOperationException("Mismatch of types in EmitCopy call");
            }
            else
            {
                if (!toField.FieldType.IsAssignableFrom(fromField.FieldType))
                    throw new InvalidOperationException("Mismatch of types in EmitCopy call");
            }

            if (toField.FieldType.IsPrimitive || toField.FieldType == typeof(string))
            {
                generator.Emit(OpCodes.Ldarg, toArg);

                generator.Emit(OpCodes.Ldarg, fromArg);
                generator.EmitLdfld(fromField);

                generator.EmitStfld(toField);
            }
            else
            {
                if (toField.FieldType.IsValueType)
                {
                    generator.Emit(OpCodes.Ldarg, toArg);
                }

                generator.Emit(OpCodes.Ldarg, mgrArg);
                generator.Emit(OpCodes.Ldarg, fromArg);
                generator.EmitLdfld(fromField);
                generator.Emit(OpCodes.Ldarg, toArg);
                generator.EmitLdfld(toField);

                var copyMethod = typeof(ISerializationManager).GetMethod(nameof(ISerializationManager.Copy));
                Debug.Assert(copyMethod != null, nameof(copyMethod) + " != null");
                generator.Emit(OpCodes.Callvirt, copyMethod);


                if (toField.FieldType.IsValueType)
                {
                    generator.EmitStfld(toField);
                }
                else
                {
                    generator.Emit(OpCodes.Pop); //remove value return of serializationmanager
                }
            }
        }

        public static void EmitEquals(this ILGenerator generator, Type type, Label label)
        {
            if (type.IsPrimitive || type == typeof(string))
            {
                generator.Emit(OpCodes.Beq_S, label);
            }
            else
            {
                var equalsmethod = typeof(Object).GetMethods()
                    .First(m => m.Name == nameof(Equals) && m.GetParameters().Length == 2);
                generator.Emit(OpCodes.Call, equalsmethod);
                generator.Emit(OpCodes.Brtrue_S, label);
            }
        }

        public static void EmitStfld(this ILGenerator generator,
            AbstractFieldInfo fieldDefinition)
        {
            switch (fieldDefinition)
            {
                case SpecificFieldInfo field:
                    generator.Emit(OpCodes.Stfld, field.FieldInfo);
                    break;
                case SpecificPropertyInfo property:
                    generator.Emit(OpCodes.Call, property.PropertyInfo.SetMethod!); //todo paul enforce setter!!!
                    break;
            }
        }

        public static void EmitLdfld(this ILGenerator generator,
            AbstractFieldInfo fieldDefinition)
        {
            switch (fieldDefinition)
            {
                case SpecificFieldInfo field:
                    generator.Emit(OpCodes.Ldfld, field.FieldInfo);
                    break;
                case SpecificPropertyInfo property:
                    generator.Emit(OpCodes.Call, property.PropertyInfo.GetMethod!); //todo paul enforce getter!!!
                    break;
            }
        }

        public static void EmitSerializeField(this ILGenerator generator,
            SerializationDataDefinition.FieldDefinition fieldDefinition, int defaultValueIdx)
        {
            if(fieldDefinition.Attribute.ReadOnly) return; //hehe ez pz

            var isDefaultLabel = generator.DefineLabel();
            var alwaysWriteLabel = generator.DefineLabel();

            generator.Emit(OpCodes.Ldarg, 4); //load alwaysWrite bool
            generator.Emit(OpCodes.Brtrue_S, alwaysWriteLabel); //skip defaultcheck if alwayswrite is true

            //skip all of this if the value is default
            generator.Emit(OpCodes.Ldarg_0);
            generator.EmitLdfld(fieldDefinition.FieldInfo);
            generator.Emit(OpCodes.Stloc_0); //also storing the value for later so we dont have to do ldfld again
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Ldarg_3);
            generator.Emit(OpCodes.Ldc_I4, defaultValueIdx);
            generator.Emit(OpCodes.Ldelem, fieldDefinition.FieldType);
            generator.EmitEquals(fieldDefinition.FieldType, isDefaultLabel);

            generator.MarkLabel(alwaysWriteLabel);

            if (fieldDefinition.FieldType.IsPrimitive || fieldDefinition.FieldType == typeof(string))
            {
                generator.Emit(OpCodes.Ldarg_1); // loading serializer
                generator.Emit(OpCodes.Ldstr, fieldDefinition.Attribute.Tag); //loading tag
                generator.Emit(OpCodes.Ldloc_0); //loading value
                generator.Emit(OpCodes.Ldarg_3);
                generator.Emit(OpCodes.Ldc_I4, defaultValueIdx);
                generator.Emit(OpCodes.Ldelem, fieldDefinition.FieldType);

                var writedatafieldmethod = typeof(ObjectSerializer)
                    .GetMethod(nameof(ObjectSerializer.WriteDataField))?.MakeGenericMethod(fieldDefinition.FieldType);
                Debug.Assert(writedatafieldmethod != null, nameof(writedatafieldmethod) + " != null");
                generator.Emit(OpCodes.Callvirt, writedatafieldmethod); //calling writedatafield
            }
            else
            {
                generator.Emit(OpCodes.Ldarg_2); //load serv3mgr
                generator.Emit(OpCodes.Ldobj, fieldDefinition.FieldType); //loading type
                generator.Emit(OpCodes.Ldloc_0); //loading object
                generator.Emit(OpCodes.Ldarg_1); //loading serializer
                generator.Emit(OpCodes.Ldarg, 4); //loading alwaysWrite flag

                var serializeMethod = typeof(ISerializationManager).GetMethod(nameof(ISerializationManager.Serialize));
                Debug.Assert(serializeMethod != null, nameof(serializeMethod) + " != null");
                generator.Emit(OpCodes.Callvirt, serializeMethod);
            }

            generator.MarkLabel(isDefaultLabel);
        }

        public static void EmitExposeDataCall(this ILGenerator generator)
        {
            var exposeDataMethod = typeof(IExposeData).GetMethod(nameof(IExposeData.ExposeData));
            Debug.Assert(exposeDataMethod != null, nameof(exposeDataMethod) + " != null");
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Callvirt, exposeDataMethod);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using Robust.Shared.Log;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;
using Robust.Shared.Utility;
using YamlDotNet.Serialization.NamingConventions;
using static Robust.Shared.Serialization.Manager.SerializationManager;

namespace Robust.Shared.Serialization.Manager.Definition
{
    public abstract class DataDefinition
    {
        internal ImmutableArray<FieldDefinition> BaseFieldDefinitions { get; init; }
        internal bool IsRecord { get; init; }

        public abstract bool TryGetDuplicates([NotNullWhen(true)] out string[] duplicates);
    }

    internal sealed partial class DataDefinition<T> : DataDefinition where T : notnull
    {
        private readonly struct FieldInterfaceInfo
        {
            public readonly (bool Value, bool Sequence, bool Mapping) Reader;
            public readonly bool Writer;
            public readonly bool Copier;
            public readonly bool CopyCreator;
            public readonly (bool Value, bool Sequence, bool Mapping) Validator;


            public FieldInterfaceInfo((bool Value, bool Sequence, bool Mapping) reader, bool writer, bool copier, bool copyCreator, (bool Value, bool Sequence, bool Mapping) validator)
            {
                Reader = reader;
                Writer = writer;
                Copier = copier;
                CopyCreator = copyCreator;
                Validator = validator;
            }
        }

        internal readonly PopulateDelegateSignature Populate;
        internal readonly SerializeDelegateSignature Serialize;
        internal readonly CopyDelegateSignature CopyTo;

        //todo paul InstantiationDelegate
        [UsedImplicitly]
        internal DataDefinition(SerializationManager manager, bool isRecord)
        {
            IsRecord = isRecord;

            var fieldDefs = GetFieldDefinitions(manager, isRecord);

            var dataFields = fieldDefs
                .Select(f => f.Attribute)
                .OfType<DataFieldAttribute>().ToArray();

            Duplicates = dataFields
                .Where(f =>
                    dataFields.Count(df => df.Tag == f.Tag) > 1)
                .Select(f => f.Tag)
                .Distinct()
                .ToArray();

            var fields = fieldDefs;

            fields.Sort((a, b) => b.Attribute.Priority.CompareTo(a.Attribute.Priority));

            BaseFieldDefinitions = fields.ToImmutableArray();

            DefaultValues = fieldDefs.Select(f => f.DefaultValue).ToArray();
            var fieldAssigners = new object[BaseFieldDefinitions.Length];
            var fieldAccessors = new object[BaseFieldDefinitions.Length];
            var fieldValidators = new ValidateFieldDelegate[BaseFieldDefinitions.Length];

            var interfaceInfos = new FieldInterfaceInfo[BaseFieldDefinitions.Length];

            for (var i = 0; i < BaseFieldDefinitions.Length; i++)
            {
                var fieldDefinition = BaseFieldDefinitions[i];
                fieldAssigners[i] = InternalReflectionUtils.EmitFieldAssigner<T>(fieldDefinition.BackingField);
                fieldAccessors[i] = InternalReflectionUtils.EmitFieldAccessor(typeof(T), fieldDefinition);

                if (fieldDefinition.Attribute.CustomTypeSerializer != null)
                {
                    //reader (value, sequence, mapping), writer, copier
                    var reader = (false, false, false);
                    var writer = false;
                    var copier = false;
                    var copyCreator = false;
                    var validator = (false, false, false);
                    foreach (var @interface in fieldDefinition.Attribute.CustomTypeSerializer.GetInterfaces())
                    {
                        var genericTypedef = @interface.GetGenericTypeDefinition();
                        if (genericTypedef == typeof(ITypeWriter<>))
                        {
                            if (@interface.GenericTypeArguments[0].IsAssignableTo(fieldDefinition.FieldType))
                            {
                                writer = true;
                            }
                        }
                        else if (genericTypedef == typeof(ITypeCopier<>))
                        {
                            if (@interface.GenericTypeArguments[0].IsAssignableTo(fieldDefinition.FieldType))
                            {
                                copier = true;
                            }
                        }
                        else if (genericTypedef == typeof(ITypeCopyCreator<>))
                        {
                            if (@interface.GenericTypeArguments[0].IsAssignableTo(fieldDefinition.FieldType))
                            {
                                copyCreator = true;
                            }
                        }
                        else if (genericTypedef == typeof(ITypeReader<,>))
                        {
                            if (@interface.GenericTypeArguments[0].IsAssignableTo(fieldDefinition.FieldType))
                            {
                                if (@interface.GenericTypeArguments[1] == typeof(ValueDataNode))
                                {
                                    reader.Item1 = true;
                                }
                                else if (@interface.GenericTypeArguments[1] == typeof(SequenceDataNode))
                                {
                                    reader.Item2 = true;
                                }
                                else if (@interface.GenericTypeArguments[1] == typeof(MappingDataNode))
                                {
                                    reader.Item3 = true;
                                }
                            }
                        }
                        else if (genericTypedef == typeof(ITypeValidator<,>))
                        {
                            if (@interface.GenericTypeArguments[0].IsAssignableTo(fieldDefinition.FieldType))
                            {
                                if (@interface.GenericTypeArguments[1] == typeof(ValueDataNode))
                                {
                                    validator.Item1 = true;
                                }
                                else if (@interface.GenericTypeArguments[1] == typeof(SequenceDataNode))
                                {
                                    validator.Item2 = true;
                                }
                                else if (@interface.GenericTypeArguments[1] == typeof(MappingDataNode))
                                {
                                    validator.Item3 = true;
                                }
                            }
                        }
                    }

                    if (!reader.Item1 && !reader.Item2 && !reader.Item3 && !writer && !copier && !validator.Item1 && !validator.Item2 && !validator.Item3)
                    {
                        throw new InvalidOperationException(
                            $"Could not find any fitting implementation of ITypeReader, ITypeWriter or ITypeCopier for field {fieldDefinition}({fieldDefinition.FieldType}) on type {typeof(T)} on CustomTypeSerializer {fieldDefinition.Attribute.CustomTypeSerializer}");
                    }

                    interfaceInfos[i] = new FieldInterfaceInfo(reader, writer, copier, copyCreator, validator);
                }
            }

            FieldInterfaceInfos = interfaceInfos.ToImmutableArray();
            FieldAssigners = fieldAssigners.ToImmutableArray();
            FieldAccessors = fieldAccessors.ToImmutableArray();

            for (int i = 0; i < BaseFieldDefinitions.Length; i++)
            {
                //has to be done after fieldinterfaceinfos are done
                fieldValidators[i] = EmitFieldValidationDelegate(manager, i);
            }

            FieldValidators = fieldValidators.ToImmutableArray();

            Populate = EmitPopulateDelegate(manager);
            Serialize = EmitSerializeDelegate(manager);
            CopyTo = EmitCopyDelegate(manager);
        }

        private string[] Duplicates { get; }
        private object?[] DefaultValues { get; }

        private ImmutableArray<FieldInterfaceInfo> FieldInterfaceInfos { get; }

        private ImmutableArray<object> FieldAssigners { get; }
        private ImmutableArray<object> FieldAccessors { get; }

        private ImmutableArray<ValidateFieldDelegate> FieldValidators { get; }

        private bool TryGetIndex(string tag, out int index)
        {
            for (index = 0; index < BaseFieldDefinitions.Length; index++)
            {
                if (BaseFieldDefinitions[index].Attribute is DataFieldAttribute dataFieldAttribute &&
                    dataFieldAttribute.Tag == tag)
                    return true;
            }

            return false;
        }

        public ValidationNode Validate(
            ISerializationManager serialization,
            MappingDataNode mapping,
            ISerializationContext? context)
        {
            var validatedMapping = new Dictionary<ValidationNode, ValidationNode>();

            foreach (var (key, val) in mapping.Children)
            {
                if (key is not ValueDataNode valueDataNode)
                {
                    validatedMapping.Add(new ErrorNode(key, "Key not ValueDataNode."), new InconclusiveNode(val));
                    continue;
                }

                if (TryGetIndex(valueDataNode.Value, out var idx))
                {
                    var error = new ErrorNode(
                        key,
                        $"Field \"{valueDataNode.Value}\" not found in \"{typeof(T)}\".",
                        false);

                    validatedMapping.Add(error, new InconclusiveNode(val));
                    continue;
                }

                var keyValidated = serialization.ValidateNode(typeof(string), key, context);

                if (IsNull(val))
                {
                    if (!NullableHelper.IsMarkedAsNullable(BaseFieldDefinitions[idx].FieldInfo))
                    {
                        var error = new ErrorNode(
                            val,
                            $"Field \"{valueDataNode.Value}\" had null value despite not being annotated as nullable.");

                        validatedMapping.Add(keyValidated, error);
                    }
                    else
                    {
                        validatedMapping.Add(keyValidated, new ValidatedValueNode(val));
                    }

                    continue;
                }

                validatedMapping.Add(keyValidated, FieldValidators[idx](val, context));
            }

            return new ValidatedMappingNode(validatedMapping);
        }

        public override bool TryGetDuplicates([NotNullWhen(true)] out string[] duplicates)
        {
            duplicates = Duplicates;
            return duplicates.Length > 0;
        }

        private bool GatherFieldData(AbstractFieldInfo fieldInfo, out DataFieldBaseAttribute? dataFieldBaseAttribute,
            [NotNullWhen(true)]out AbstractFieldInfo? backingField, out InheritanceBehavior inheritanceBehavior)
        {
            dataFieldBaseAttribute = null;
            backingField = fieldInfo;
            inheritanceBehavior = InheritanceBehavior.Default;

            if (fieldInfo.HasAttribute<AlwaysPushInheritanceAttribute>(true))
            {
                inheritanceBehavior = InheritanceBehavior.Always;
            }
            else if (fieldInfo.HasAttribute<NeverPushInheritanceAttribute>(true))
            {
                inheritanceBehavior = InheritanceBehavior.Never;
            }

            if (fieldInfo is SpecificPropertyInfo propertyInfo)
            {
                // We only want the most overriden instance of a property for the type we are working with
                if (!propertyInfo.IsMostOverridden(typeof(T)))
                {
                    return false;
                }

                if (propertyInfo.PropertyInfo.GetMethod == null)
                {
                    Logger.ErrorS(LogCategory, $"Property {propertyInfo} is annotated with DataFieldAttribute but has no getter");
                    return false;
                }
            }

            if (!fieldInfo.TryGetAttribute<DataFieldAttribute>(out var dataFieldAttribute, true))
            {
                if (!fieldInfo.TryGetAttribute<IncludeDataFieldAttribute>(out var includeDataFieldAttribute, true))
                {
                    return true;
                }
                dataFieldBaseAttribute = includeDataFieldAttribute;
            }
            else
            {
                dataFieldBaseAttribute = dataFieldAttribute;

                if (fieldInfo is SpecificPropertyInfo property && !dataFieldAttribute.ReadOnly && property.PropertyInfo.SetMethod == null)
                {
                    if (!property.TryGetBackingField(out var backingFieldInfo))
                    {
                        Logger.ErrorS(LogCategory, $"Property {property} in type {property.DeclaringType} is annotated with DataFieldAttribute as non-readonly but has no auto-setter");
                        return false;
                    }

                    backingField = backingFieldInfo;
                }
            }

            return true;
        }

        private List<FieldDefinition> GetFieldDefinitions(SerializationManager manager, bool isRecord)
        {
            var dummyObject = manager.GetOrCreateInstantiator<T>(isRecord)();
            var fieldDefinitions = new List<FieldDefinition>();

            foreach (var abstractFieldInfo in typeof(T).GetAllPropertiesAndFields())
            {
                if (abstractFieldInfo.IsBackingField())
                    continue;

                if (isRecord && abstractFieldInfo.IsAutogeneratedRecordMember())
                    continue;

                if (!GatherFieldData(abstractFieldInfo, out var dataFieldBaseAttribute, out var backingField,
                        out var inheritanceBehavior))
                    continue;

                if (dataFieldBaseAttribute == null)
                {
                    if (!isRecord)
                        continue;

                    dataFieldBaseAttribute = new DataFieldAttribute(CamelCaseNamingConvention.Instance.Apply(abstractFieldInfo.Name));
                }

                var fieldDefinition = new FieldDefinition(
                    dataFieldBaseAttribute,
                    abstractFieldInfo.GetValue(dummyObject),
                    abstractFieldInfo,
                    backingField,
                    inheritanceBehavior);

                fieldDefinitions.Add(fieldDefinition);
            }

            return fieldDefinitions;
        }
    }
}

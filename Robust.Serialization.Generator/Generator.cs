﻿using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Robust.Serialization.Generator;

[Generator]
public class Generator : IIncrementalGenerator
{
    private const string DataDefinitionNamespace = "Robust.Shared.Serialization.Manager.Attributes.DataDefinitionAttribute";
    private const string ImplicitDataDefinitionNamespace = "Robust.Shared.Serialization.Manager.Attributes.ImplicitDataDefinitionForInheritorsAttribute";
    private const string MeansDataDefinitionNamespace = "Robust.Shared.Serialization.Manager.Attributes.MeansDataDefinitionAttribute";
    private const string DataFieldNamespace = "Robust.Shared.Serialization.Manager.Attributes.DataFieldAttribute";
    private const string ComponentNamespace = "Robust.Shared.GameObjects.Component";
    private const string ComponentInterfaceNamespace = "Robust.Shared.GameObjects.IComponent";

    private static readonly DiagnosticDescriptor DataDefinitionPartialRule = new(
        Diagnostics.IdDataDefinitionPartial,
        "Type must be partial",
        "Type {0} has a DataDefinition attribute but is not partial.",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Make sure to mark any type that is a data definition as partial."
    );

    private static readonly DiagnosticDescriptor NestedDataDefinitionPartialRule = new(
        Diagnostics.IdDataDefinitionPartial,
        "Type must be partial",
        "Type {0} contains nested data definition {1} but is not partial.",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Make sure to mark any type containing a nested data definition as partial."
    );

    private static readonly DiagnosticDescriptor DataFieldWritableRule = new(
        Diagnostics.IdDataDefinitionPartial,
        "Data field must not be readonly",
        "Field {0} in data definition {1} is marked as a DataField but is readonly.",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Make sure to add a setter or remove the readonly modifier."
    );

    public void Initialize(IncrementalGeneratorInitializationContext initContext)
    {
        IncrementalValuesProvider<TypeDeclarationSyntax> dataDefinitions = initContext.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is TypeDeclarationSyntax,
            static (context, _) =>
            {
                var type = (TypeDeclarationSyntax) context.Node;
                var symbol = (ITypeSymbol) context.SemanticModel.GetDeclaredSymbol(type)!;
                return IsDataDefinition(symbol) ? type : null;
            }
        ).Where(static type => type != null)!;

        var comparer = new DataDefinitionComparer();
        initContext.RegisterSourceOutput(
            initContext.CompilationProvider.Combine(dataDefinitions.WithComparer(comparer).Collect()),
            static (sourceContext, source) =>
            {
                var (compilation, types) = source;
                var builder = new StringBuilder();
                var containingTypes = new Stack<INamedTypeSymbol>();

                foreach (var type in types)
                {
                    builder.Clear();
                    containingTypes.Clear();

                    var symbol = (ITypeSymbol) compilation.GetSemanticModel(type.SyntaxTree).GetDeclaredSymbol(type)!;

                    if (type.Modifiers.IndexOf(SyntaxKind.PartialKeyword) == -1)
                    {
                        sourceContext.ReportDiagnostic(Diagnostic.Create(DataDefinitionPartialRule, type.Keyword.GetLocation(), symbol.Name));
                        continue;
                    }

                    var namespaceString = symbol.ContainingNamespace.IsGlobalNamespace
                        ? string.Empty
                        : $"namespace {symbol.ContainingNamespace.ToDisplayString()};";

                    builder.AppendLine($"""
#nullable enable
using Robust.Shared.Analyzers;
using Robust.Shared.IoC;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;

{namespaceString}
""");

                    var containingType = symbol.ContainingType;
                    while (containingType != null)
                    {
                        containingTypes.Push(containingType);
                        containingType = containingType.ContainingType;
                    }

                    var nonPartial = false;
                    foreach (var parent in containingTypes)
                    {
                        var syntax = (ClassDeclarationSyntax) parent.DeclaringSyntaxReferences[0].GetSyntax();
                        if (syntax.Modifiers.IndexOf(SyntaxKind.PartialKeyword) == -1)
                        {
                            sourceContext.ReportDiagnostic(Diagnostic.Create(NestedDataDefinitionPartialRule, syntax.Keyword.GetLocation(), parent.Name, symbol.Name));
                            nonPartial = true;
                            continue;
                        }

                        builder.AppendLine($"{GetPartialTypeDefinitionLine(parent)}\n{{");
                    }

                    if (nonPartial)
                        continue;

                    var definition = GetDataFields(symbol);

                    builder.Append($$"""
[RobustAutoGenerated]
{{GetPartialTypeDefinitionLine(symbol)}} : ISerializationGenerated<{{definition.GenericTypeName}}>
{
    {{GetCopyConstructor(definition, sourceContext)}}

    {{GetCopyMethod(definition, sourceContext)}}

    {{GetInstantiator(definition)}}
}
"""
                    );

                    for (var i = 0; i < containingTypes.Count; i++)
                    {
                        builder.AppendLine("}");
                    }

                    var symbolName = symbol
                        .ToDisplayString()
                        .Replace('<', '{')
                        .Replace('>', '}');

                    var sourceText = CSharpSyntaxTree
                        .ParseText(builder.ToString())
                        .GetRoot()
                        .NormalizeWhitespace()
                        .ToFullString();

                    sourceContext.AddSource($"{symbolName}.g.cs", sourceText);
                }
            }
        );
    }

    private static string GetPartialTypeDefinitionLine(ITypeSymbol symbol)
    {
        var access = symbol.DeclaredAccessibility switch
        {
            Accessibility.Private => "private",
            Accessibility.ProtectedAndInternal => "protected internal",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.Public => "public",
            _ => "public"
        };

        string typeKeyword;
        if (symbol.IsRecord)
        {
            typeKeyword = symbol.IsValueType ? "record struct" : "record";
        }
        else
        {
            typeKeyword = symbol.IsValueType ? "struct" : "class";
        }

        var abstractKeyword = symbol.IsAbstract ? "abstract " : string.Empty;
        var typeName = GetGenericTypeName(symbol);
        return $"{access} {abstractKeyword}partial {typeKeyword} {typeName}";
    }

    private static string GetGenericTypeName(ITypeSymbol symbol)
    {
        var name = symbol.Name;

        if (symbol is INamedTypeSymbol { TypeParameters: { Length: > 0 } parameters })
        {
            name += "<";

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                name += parameter.Name;

                if (i < parameters.Length - 1)
                {
                    name += ", ";
                }
            }

            name += ">";
        }

        return name;
    }

    private static DataDefinition GetDataFields(ITypeSymbol symbol)
    {
        var fields = new List<DataField>();
        var otherFields = new List<ISymbol>();

        foreach (var member in symbol.GetMembers())
        {
            if (member is not IFieldSymbol && member is not IPropertySymbol)
                continue;

            if (member.IsStatic)
                continue;

            if (IsDataField(member, out var type, out var attribute))
            {
                fields.Add(new DataField(member, type, attribute));
            }
            else
            {
                otherFields.Add(member);
            }
        }

        var typeName = GetGenericTypeName(symbol);
        return new DataDefinition(symbol, typeName, fields, otherFields);
    }

     private static string GetCopyConstructor(DataDefinition definition, SourceProductionContext context)
     {
         var builder = new StringBuilder();
//         builder.AppendLine($$"""
// public {{definition.Type.Name}}({{definition.GenericTypeName}} other, ISerializationManager serialization, SerializationHookContext hookCtx, ISerializationContext? context = null) : this()
// {
// """);
//
//         foreach (var field in definition.Fields)
//         {
//             var type = field.Type;
//             var typeName = type.ToDisplayString();
//             if (type is IArrayTypeSymbol { Rank: > 1 })
//             {
//                 typeName = typeName.Replace("*", "");
//             }
//
//             var isClass = type.IsReferenceType || type.SpecialType == SpecialType.System_String;
//             var isNullable = type.NullableAnnotation == NullableAnnotation.Annotated;
//             var nullableOverride = isClass && !isNullable ? ", true" : string.Empty;
//             var nullableAnnotation = isClass && isNullable ? "?" : string.Empty;
//             var nullableTypeName = typeName;//$"{typeName}{nullableAnnotation}";
//             var name = field.Symbol.Name;
//
//             builder.AppendLine($$"""
// if (serialization.TryGetCopierOrCreator<{{typeName}}>(out var {{name}}Copier, out var {{name}}CopyCreator, context))
// {
// """);
//
//             if (isClass)
//             {
//                 builder.AppendLine($$"""
//     if (other.{{name}} == null)
//     {
//         {{name}} = null!;
//     }
//     else
//     {
// """);
//             }
//
//             builder.AppendLine($$"""
//         if ({{name}}Copier != null)
//         {
//             {{nullableTypeName}} target = default!;
//             serialization.CopyTo<{{nullableTypeName}}>({{name}}Copier!, other.{{name}}, ref target, hookCtx, context{{nullableOverride}});
//             {{name}} = target!;
//         }
//         else if ({{name}}CopyCreator != null)
//         {
//             {{name}} = {{name}}CopyCreator.CreateCopy(serialization, other.{{name}}!, IoCManager.Instance!, hookCtx, context)!;
//         }
// """);
//
//             if (isClass)
//             {
//                 builder.AppendLine("}");
//             }
//
//             builder.AppendLine("""
// }
// else
// {
// """);
//
//             if (CanBeCopiedByValue(type))
//             {
//                 builder.AppendLine($"{name} = other.{name};");
//             }
//             else if (IsDataDefinition(type))
//             {
//                 var nullability = type.IsValueType ? string.Empty : "?";
//                 builder.AppendLine($"{name} = other.{name}{nullability}.Copy(serialization, hookCtx, context)!;");
//             }
//             else
//             {
//                 builder.AppendLine($"{name} = serialization.CreateCopy(other.{name}, hookCtx, context);");
//             }
//
//             builder.AppendLine("}");
//         }
//
//         builder.AppendLine("}");
//
//         if (NeedsImplicitConstructor(definition.Type))
//         {
//             builder.AppendLine($$"""
// // Implicit constructor
// {{(definition.Type.IsValueType ? "#pragma warning disable CS8618" : string.Empty)}}
// public {{definition.Type.Name}}()
// {{(definition.Type.IsValueType ? "#pragma warning enable CS8618" : string.Empty)}}
// {
// }
// """);
//         }

//          if (definition.Type.IsRecord)
//          {
//              Debugger.Launch();
//              if (definition.Type.Name.Contains("CargoBountyData"))
//              {
//              }
//
//              var thisCall = new StringBuilder();
//              // if (definition.Type.IsRecord && definition.Type is INamedTypeSymbol namedSymbol)
//              // {
//              //     foreach (var constructor in namedSymbol.InstanceConstructors)
//              //     {
//              //         if (constructor.IsImplicitlyDeclared)
//              //             continue;
//              //
//              //         thisCall.Append(": this(");
//              //
//              //         for (var i = 0; i < constructor.Parameters.Length; i++)
//              //         {
//              //             var parameter = constructor.Parameters[i];
//              //             var parameterType = parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
//              //             thisCall.Append($"default({parameterType})");
//              //
//              //             if (i + 1 < constructor.Parameters.Length)
//              //             {
//              //                 thisCall.Append(", ");
//              //             }
//              //         }
//              //
//              //         thisCall.Append(")");
//              //
//              //         break;
//              //     }
//              // }
//
//              thisCall.Append($" : this(target)");
//
//              builder.AppendLine($$"""
//                                   public {{definition.Type.Name}}({{definition.GenericTypeName}} target, ISerializationManager serialization, SerializationHookContext hookCtx, ISerializationContext? context = null){{thisCall}}
//                                   {
//                                   """);
//
//              CopyDataFields(builder, definition, context);
//
//              foreach (var field in definition.OtherFields)
//              {
//                  if (field.IsImplicitlyDeclared)
//                      continue;
//
//                  var name = field.Name;
//                  builder.AppendLine($"{name} = target.{name};");
//              }
//
//              builder.AppendLine("}");
//          }

         if (NeedsEmptyConstructor(definition.Type))
         {
             builder.AppendLine($$"""
                                  // Implicit constructor
                                  {{(definition.Type.IsValueType ? "#pragma warning disable CS8618" : string.Empty)}}
                                  public {{definition.Type.Name}}()
                                  {{(definition.Type.IsValueType ? "#pragma warning enable CS8618" : string.Empty)}}
                                  {
                                  }
                                  """);
         }

         return builder.ToString();
     }

    private static string GetCopyMethod(DataDefinition definition, SourceProductionContext context)
    {
        var builder = new StringBuilder();

        var baseType = definition.Type;
        var hasDataDefinitionBaseType = false;
        while ((baseType = baseType.BaseType) != null)
        {
            if (IsDataDefinition(baseType))
            {
                hasDataDefinitionBaseType = true;
                break;
            }
        }

        builder.AppendLine($$"""
                             public void Copy(ref {{definition.GenericTypeName}} target, ISerializationManager serialization, SerializationHookContext hookCtx, ISerializationContext? context = null)
                             {
                             """);

        CopyDataFields(builder, definition, context);

        builder.AppendLine("}");

        if (ImplementsInterface(definition.Type, ComponentInterfaceNamespace))
        {
            builder.AppendLine($$"""
                                 public override void Copy(ref IComponent target, ISerializationManager serialization, SerializationHookContext hookCtx, ISerializationContext? context = null)
                                 {
                                     base.Copy(ref target, serialization, hookCtx, context);
                                     var comp = ({{definition.GenericTypeName}}) target;
                                     Copy(ref comp, serialization, hookCtx, context);
                                     target = comp;
                                 }
                                 """);
        }

        return builder.ToString();
    }

    private static string GetInstantiator(DataDefinition definition)
    {
        var builder = new StringBuilder();
        var modifiers = string.Empty;

        if (definition.Type.IsAbstract)
        {
            modifiers += "abstract ";
        }

        if (definition.Type.BaseType is { } baseType)
        {
            if (IsDataDefinition(baseType) || baseType.ToDisplayString() == ComponentNamespace)
            {
                modifiers += "override ";
            }
        }

        if (modifiers == string.Empty && definition.Type.IsReferenceType && !definition.Type.IsSealed)
            modifiers = "virtual ";

        if (definition.Type.IsAbstract)
        {
            builder.AppendLine($"public {modifiers}{definition.GenericTypeName} Instantiate();");
        }
        else
        {
            builder.AppendLine($$"""
                                 public {{modifiers}}{{definition.GenericTypeName}} Instantiate()
                                 {
                                     return new {{definition.GenericTypeName}}();
                                 }
                                 """);
        }


        return builder.ToString();
    }

    private static bool IsDataDefinition(ITypeSymbol type)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == DataDefinitionNamespace)
                return true;
        }

        var baseType = type.BaseType;
        while (baseType != null)
        {
            foreach (var attribute in baseType.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() == ImplicitDataDefinitionNamespace)
                    return true;
            }

            baseType = baseType.BaseType;
        }

        return false;
    }

    private static bool IsNullableType(ITypeSymbol type)
    {
        if (type.NullableAnnotation == NullableAnnotation.Annotated)
            return true;

        if (type.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
            return true;

        return false;
    }

    private static bool CanBeCopiedByValue(ITypeSymbol type)
    {
        if (type.OriginalDefinition.ToDisplayString() == "System.Nullable<T>")
            return CanBeCopiedByValue(((INamedTypeSymbol) type).TypeArguments[0]);

        if (type.TypeKind == TypeKind.Enum)
            return true;

        switch (type.SpecialType)
        {
            case SpecialType.System_Enum:
            case SpecialType.System_Boolean:
            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Decimal:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_String:
            case SpecialType.System_DateTime:
                return true;
            default:
                return false;
        }
    }

    private static bool NeedsEmptyConstructor(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
            return false;

        if (named.InstanceConstructors.Length == 0)
            return true;

        foreach (var constructor in named.InstanceConstructors)
        {
            if (constructor.Parameters.Length == 0 && !constructor.IsImplicitlyDeclared)
                return false;
        }

        return true;
    }

    private static bool ImplementsInterface(ITypeSymbol type, string interfaceName)
    {
        foreach (var @interface in type.AllInterfaces)
        {
            if (@interface.ToDisplayString() == interfaceName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReadOnlyMember(ISymbol member)
    {
        if (member is IFieldSymbol field)
        {
            return field.IsReadOnly;
        }
        else if (member is IPropertySymbol property)
        {
            return property.SetMethod == null;
        }

        return false;
    }

    private static bool IsDataField(ISymbol member, out ITypeSymbol type, out AttributeData attribute)
    {
        // TODO data records
        if (member is IFieldSymbol field)
        {
            foreach (var attr in field.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == DataFieldNamespace)
                {
                    type = field.Type;
                    attribute = attr;
                    return true;
                }
            }
        }
        else if (member is IPropertySymbol property)
        {
            foreach (var attr in property.GetAttributes())
            {
                if (attr.AttributeClass?.ToDisplayString() == DataFieldNamespace)
                {
                    type = property.Type;
                    attribute = attr;
                    return true;
                }
            }
        }

        type = null!;
        attribute = null!;
        return false;
    }

    private static void CopyDataFields(StringBuilder builder, DataDefinition definition, SourceProductionContext context)
    {
        var structCopier = new StringBuilder();

        for (var i = 0; i < definition.Fields.Count; i++)
        {
            var field = definition.Fields[i];

            if (IsReadOnlyMember(field.Symbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(DataFieldWritableRule, field.Symbol.Locations.First(),
                    field.Symbol.Name, definition.Type.Name));
                continue;
            }

            var type = field.Type;
            var typeName = type.ToDisplayString();
            if (type is IArrayTypeSymbol { Rank: > 1 })
            {
                typeName = typeName.Replace("*", "");
            }

            var isClass = type.IsReferenceType || type.SpecialType == SpecialType.System_String;
            var isNullable = type.NullableAnnotation == NullableAnnotation.Annotated;
            var nullableOverride = isClass && !isNullable ? ", true" : string.Empty;
            var name = field.Symbol.Name;
            var tempVarName = $"{name}Temp";

            builder.AppendLine($$"""
                                 {{typeName}} {{tempVarName}} = default!;
                                 if (serialization.TryGetCopierOrCreator<{{typeName}}>(out var {{name}}Copier, out var {{name}}CopyCreator, context))
                                 {
                                 """);

            if (isClass)
            {
                builder.AppendLine($$"""
                                         if ({{name}} == null)
                                         {
                                             {{tempVarName}} = null!;
                                         }
                                         else
                                         {
                                     """);
            }

            builder.AppendLine($$"""
                                         if ({{name}}Copier != null)
                                         {
                                             {{typeName}} temp = default!;
                                             serialization.CopyTo<{{typeName}}>({{name}}Copier!, {{name}}, ref temp, hookCtx, context{{nullableOverride}});
                                             {{tempVarName}} = temp!;
                                         }
                                         else
                                         {
                                             {{tempVarName}} = {{name}}CopyCreator!.CreateCopy(serialization, {{name}}!, IoCManager.Instance!, hookCtx, context)!;
                                         }
                                 """);

            if (isClass)
            {
                builder.AppendLine("}");
            }

            builder.AppendLine("""
                               }
                               else
                               {
                               """);

            if (CanBeCopiedByValue(type))
            {
                builder.AppendLine($"{tempVarName} = {name};");
            }
            else if (IsDataDefinition(type) && !type.IsAbstract &&
                     type is not INamedTypeSymbol { TypeKind: TypeKind.Interface })
            {
                var nullability = type.IsValueType ? string.Empty : "?";
                var orNew = type.IsReferenceType
                    ? $" ?? {name}{nullability}.Instantiate()"
                    : string.Empty; // TODO nullable structs
                var nullable = !type.IsValueType || IsNullableType(type);


                builder.AppendLine($"var temp = {name}{orNew};");

                if (nullable)
                {
                    builder.AppendLine("""
                                       if (temp != null)
                                       {
                                       """);
                }

                builder.AppendLine($$"""
                                     {{name}}{{nullability}}.Copy(ref temp, serialization, hookCtx, context);
                                     {{tempVarName}} = temp;
                                     """);

                if (nullable)
                {
                    builder.AppendLine("}");
                }
            }
            else
            {
                builder.AppendLine($"{tempVarName} = serialization.CreateCopy({name}, hookCtx, context);");
            }

            builder.AppendLine("}");

            if (definition.Type.IsValueType)
            {
                structCopier.AppendLine($"{name} = {tempVarName},");
            }
            else
            {
                builder.AppendLine($"target.{name} = {tempVarName};");
            }
        }

        if (definition.Type.IsValueType)
        {
            builder.AppendLine($$"""
                                target = target with
                                {
                                    {{structCopier}}
                                };
                                """);
        }
    }
}

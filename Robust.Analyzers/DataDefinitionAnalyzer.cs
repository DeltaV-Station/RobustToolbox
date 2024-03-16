﻿#nullable enable
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Robust.Roslyn.Shared;

namespace Robust.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DataDefinitionAnalyzer : DiagnosticAnalyzer
{
    private const string DataDefinitionNamespace = "Robust.Shared.Serialization.Manager.Attributes.DataDefinitionAttribute";
    private const string ImplicitDataDefinitionNamespace = "Robust.Shared.Serialization.Manager.Attributes.ImplicitDataDefinitionForInheritorsAttribute";
    private const string DataFieldBaseNamespace = "Robust.Shared.Serialization.Manager.Attributes.DataFieldBaseAttribute";

    private static readonly DiagnosticDescriptor DataDefinitionPartialRule = new(
        Diagnostics.IdDataDefinitionPartial,
        "Type must be partial",
        "Type {0} is a DataDefinition but is not partial",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Make sure to mark any type that is a data definition as partial."
    );

    private static readonly DiagnosticDescriptor NestedDataDefinitionPartialRule = new(
        Diagnostics.IdNestedDataDefinitionPartial,
        "Type must be partial",
        "Type {0} contains nested data definition {1} but is not partial",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Make sure to mark any type containing a nested data definition as partial."
    );

    private static readonly DiagnosticDescriptor DataFieldWritableRule = new(
        Diagnostics.IdDataFieldWritable,
        "Data field must not be readonly",
        "Data field {0} in data definition {1} is readonly",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Make sure to remove the readonly modifier."
    );

    private static readonly DiagnosticDescriptor DataFieldPropertyWritableRule = new(
        Diagnostics.IdDataFieldPropertyWritable,
        "Data field property must have a setter",
        "Data field property {0} in data definition {1} does not have a setter",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Make sure to add a setter."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        DataDefinitionPartialRule, NestedDataDefinitionPartialRule, DataFieldWritableRule, DataFieldPropertyWritableRule
    );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeDataDefinition, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeDataDefinition, SyntaxKind.StructDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeDataDefinition, SyntaxKind.RecordDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeDataDefinition, SyntaxKind.RecordStructDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeDataDefinition, SyntaxKind.InterfaceDeclaration);

        context.RegisterSyntaxNodeAction(AnalyzeDataField, SyntaxKind.FieldDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeDataFieldProperty, SyntaxKind.PropertyDeclaration);
    }

    private void AnalyzeDataDefinition(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not TypeDeclarationSyntax declaration)
            return;

        var type = context.SemanticModel.GetDeclaredSymbol(declaration)!;
        if (!IsDataDefinition(type))
            return;

        if (!IsPartial(declaration))
        {
            context.ReportDiagnostic(Diagnostic.Create(DataDefinitionPartialRule, declaration.Keyword.GetLocation(), type.Name));
        }

        var containingType = type.ContainingType;
        while (containingType != null)
        {
            var containingTypeDeclaration = (TypeDeclarationSyntax) containingType.DeclaringSyntaxReferences[0].GetSyntax();
            if (!IsPartial(containingTypeDeclaration))
            {
                context.ReportDiagnostic(Diagnostic.Create(NestedDataDefinitionPartialRule, containingTypeDeclaration.Keyword.GetLocation(), containingType.Name, type.Name));
            }

            containingType = containingType.ContainingType;
        }
    }

    private void AnalyzeDataField(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not FieldDeclarationSyntax field)
            return;

        var typeDeclaration = field.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (typeDeclaration == null)
            return;

        var type = context.SemanticModel.GetDeclaredSymbol(typeDeclaration)!;
        if (!IsDataDefinition(type))
            return;

        foreach (var variable in field.Declaration.Variables)
        {
            var fieldSymbol = context.SemanticModel.GetDeclaredSymbol(variable);
            if (fieldSymbol == null)
                continue;

            if (IsReadOnlyDataField(type, fieldSymbol))
            {
                context.ReportDiagnostic(Diagnostic.Create(DataFieldWritableRule, context.Node.GetLocation(), fieldSymbol.Name, type.Name));
            }
        }
    }

    private void AnalyzeDataFieldProperty(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not PropertyDeclarationSyntax property)
            return;

        var typeDeclaration = property.FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (typeDeclaration == null)
            return;

        var type = context.SemanticModel.GetDeclaredSymbol(typeDeclaration)!;
        if (!IsDataDefinition(type) || type.IsRecord || type.IsValueType)
            return;

        var propertySymbol = context.SemanticModel.GetDeclaredSymbol(property);
        if (propertySymbol == null)
            return;

        if (IsReadOnlyDataField(type, propertySymbol))
        {
            context.ReportDiagnostic(Diagnostic.Create(DataFieldPropertyWritableRule, context.Node.GetLocation(), propertySymbol.Name, type.Name));
        }
    }

    private static bool IsReadOnlyDataField(ITypeSymbol type, ISymbol field)
    {
        if (!IsDataField(field, out _, out _))
            return false;

        return IsReadOnlyMember(type, field);
    }

    private static bool IsPartial(TypeDeclarationSyntax type)
    {
        return type.Modifiers.IndexOf(SyntaxKind.PartialKeyword) != -1;
    }

    private static bool IsDataDefinition(ITypeSymbol? type)
    {
        if (type == null)
            return false;

        return HasAttribute(type, DataDefinitionNamespace) ||
               IsImplicitDataDefinition(type);
    }

    private static bool IsDataField(ISymbol member, out ITypeSymbol type, out AttributeData attribute)
    {
        // TODO data records and other attributes
        if (member is IFieldSymbol field)
        {
            foreach (var attr in field.GetAttributes())
            {
                if (attr.AttributeClass != null && Inherits(attr.AttributeClass, DataFieldBaseNamespace))
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
                if (attr.AttributeClass != null && Inherits(attr.AttributeClass, DataFieldBaseNamespace))
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

    private static bool Inherits(ITypeSymbol type, string parent)
    {
        foreach (var baseType in GetBaseTypes(type))
        {
            if (baseType.ToDisplayString() == parent)
                return true;
        }

        return false;
    }

    private static bool IsReadOnlyMember(ITypeSymbol type, ISymbol member)
    {
        if (member is IFieldSymbol field)
        {
            return field.IsReadOnly;
        }
        else if (member is IPropertySymbol property)
        {
            if (property.SetMethod == null)
                return true;

            if (property.SetMethod.IsInitOnly)
                return type.IsReferenceType;

            return false;
        }

        return false;
    }

    private static bool HasAttribute(ITypeSymbol type, string attributeName)
    {
        foreach (var attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == attributeName)
                return true;
        }

        return false;
    }

    private static bool IsImplicitDataDefinition(ITypeSymbol type)
    {
        if (HasAttribute(type, ImplicitDataDefinitionNamespace))
            return true;

        foreach (var baseType in GetBaseTypes(type))
        {
            if (HasAttribute(baseType, ImplicitDataDefinitionNamespace))
                return true;
        }

        foreach (var @interface in type.AllInterfaces)
        {
            if (IsImplicitDataDefinitionInterface(@interface))
                return true;
        }

        return false;
    }

    private static bool IsImplicitDataDefinitionInterface(ITypeSymbol @interface)
    {
        if (HasAttribute(@interface, ImplicitDataDefinitionNamespace))
            return true;

        foreach (var subInterface in @interface.AllInterfaces)
        {
            if (HasAttribute(subInterface, ImplicitDataDefinitionNamespace))
                return true;
        }

        return false;
    }

    private static IEnumerable<ITypeSymbol> GetBaseTypes(ITypeSymbol type)
    {
        var baseType = type.BaseType;
        while (baseType != null)
        {
            yield return baseType;
            baseType = baseType.BaseType;
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using XamlX.Ast;
using XamlX.Emit;
using XamlX.IL;
using XamlX.Parsers;
using XamlX.Transform;
using XamlX.Transform.Transformers;
using XamlX.TypeSystem;

namespace Robust.Client.NameGenerator
{
    /// <summary>
    /// Based on https://github.com/AvaloniaUI/Avalonia.NameGenerator/blob/ecc9677a23de5cbc90af07ccac14e31c0da41d6a/src/Avalonia.NameGenerator/NameReferenceGenerator.cs
    /// Adjusted for our UI-Framework & needs.
    /// </summary>
    [Generator]
    public class XamlUiPartialClassGenerator : ISourceGenerator
    {
        private const string AttributeName = "Robust.Client.AutoGenerated.GenerateTypedNameReferencesAttribute";
        private const string AttributeFile = "GenerateTypedNameReferencesAttribute";
        private const string AttributeCode = @"// <auto-generated />
using System;
namespace Robust.Client.AutoGenerated
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class GenerateTypedNameReferencesAttribute : Attribute { }
}
";

        class NameVisitor : IXamlAstVisitor
        {
            private List<(string name, string type)> _names = new List<(string name, string type)>();

            public static List<(string name, string type)> GetNames(IXamlAstNode node)
            {
                var visitor = new NameVisitor();
                node.Visit(visitor);
                return visitor._names;
            }

            private bool IsControl(IXamlType type) => type.FullName != "System.Object" &&
                                                      (type.FullName == "Robust.Client.UserInterface.Control" ||
                                                       IsControl(type.BaseType));

            public IXamlAstNode Visit(IXamlAstNode node)
            {
                if (node is XamlAstObjectNode objectNode)
                {
                    var clrtype = objectNode.Type.GetClrType();
                    var isControl = IsControl(clrtype);
                        //clrtype.Interfaces.Any(i =>
                        //i.IsInterface && i.FullName == "Robust.Client.UserInterface.IControl");

                    if (!isControl)
                        return node;

                    foreach (var child in objectNode.Children)
                    {
                        if (child is XamlAstXamlPropertyValueNode propertyValueNode &&
                            propertyValueNode.Property is XamlAstNamePropertyReference namedProperty &&
                            namedProperty.Name == "Name" &&
                            propertyValueNode.Values.Count > 0 &&
                            propertyValueNode.Values[0] is XamlAstTextNode text)
                        {
                            var reg = (text.Text, $@"{clrtype.Namespace}.{clrtype.Name}");
                            if (!_names.Contains(reg))
                            {
                                _names.Add(reg);
                            }
                        }
                    }
                }

                return node;
            }

            public void Push(IXamlAstNode node) { }

            public void Pop() { }
        }

        private static string GenerateSourceCode(
            INamedTypeSymbol classSymbol,
            string xamlFile,
            CSharpCompilation comp)
        {
            var className = classSymbol.Name;
            var nameSpace = classSymbol.ContainingNamespace.ToDisplayString();
            var parsed = XDocumentXamlParser.Parse(xamlFile);
            var typeSystem = new RoslynTypeSystem(comp);
            var compiler =
                new XamlILCompiler(
                    new TransformerConfiguration(typeSystem, typeSystem.Assemblies[0],
                        new XamlLanguageTypeMappings(typeSystem)),
                    new XamlLanguageEmitMappings<IXamlILEmitter, XamlILNodeEmitResult>(), false);
            compiler.Transformers.Add(new TypeReferenceResolver());
            compiler.Transform(parsed);
            var initialRoot = (XamlAstObjectNode) parsed.Root;
            var names = NameVisitor.GetNames(initialRoot);
            //var names = NameVisitor.GetNames((XamlAstObjectNode)XDocumentXamlParser.Parse(xamlFile).Root);
            var namedControls = names.Select(info => "        " +
                                $"protected global::{info.type} {info.name} => " +
                                $"this.FindControl<global::{info.type}>(\"{info.name}\");");
            return $@"// <auto-generated />
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
namespace {nameSpace}
{{
    partial class {className}
    {{
{string.Join("\n", namedControls)}
    }}
}}
";
        }


        public void Execute(GeneratorExecutionContext context)
        {
            var comp = (CSharpCompilation) context.Compilation;
            if(comp.GetTypeByMetadataName(AttributeName) == null)
                context.AddSource(AttributeFile, SourceText.From(AttributeCode, Encoding.UTF8));
            if (!(context.SyntaxReceiver is NameReferenceSyntaxReceiver receiver))
            {
                return;
            }

            var symbols = UnpackAnnotatedTypes(context, comp, receiver);
            if(symbols == null)
                return;

            foreach (var typeSymbol in symbols)
            {
                var xamlFileName = $"{typeSymbol.Name}.xaml";
                var relevantXamlFile = context.AdditionalFiles.FirstOrDefault(t => t.Path.EndsWith(xamlFileName));

                if (relevantXamlFile == null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "RXN0001",
                                $"Unable to discover the relevant Robust XAML file for {typeSymbol}.",
                                "Unable to discover the relevant Robust XAML file " +
                                $"expected at {xamlFileName}",
                                "Usage",
                                DiagnosticSeverity.Error,
                                true),
                            typeSymbol.Locations[0]));
                    return;
                }

                var txt = relevantXamlFile.GetText()?.ToString();
                if (txt == null)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "RXN0002",
                                $"Unexpected empty Xaml-File was found at {xamlFileName}",
                                "Expected Content due to a Class with the same name being annotated with [GenerateTypedNameReferences].",
                                "Usage",
                                DiagnosticSeverity.Error,
                                true),
                            Location.Create(xamlFileName, new TextSpan(0,0), new LinePositionSpan(new LinePosition(0,0),new LinePosition(0,0)))));
                    return;
                }

                try
                {
                    var sourceCode = GenerateSourceCode(typeSymbol, txt, comp);
                    context.AddSource($"{typeSymbol.Name}.g.cs", SourceText.From(sourceCode, Encoding.UTF8));
                }
                catch (Exception e)
                {
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "AXN0003",
                                "Unhandled exception occured while generating typed Name references.",
                                $"Unhandled exception occured while generating typed Name references: {e}",
                                "Usage",
                                DiagnosticSeverity.Error,
                                true),
                            typeSymbol.Locations[0]));
                    return;
                }
            }
        }

        private IReadOnlyList<INamedTypeSymbol> UnpackAnnotatedTypes(in GeneratorExecutionContext context, CSharpCompilation comp, NameReferenceSyntaxReceiver receiver)
        {
            var options = (CSharpParseOptions) comp.SyntaxTrees[0].Options;
            var compilation =
                comp.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(AttributeCode, Encoding.UTF8), options));
            var symbols = new List<INamedTypeSymbol>();
            var attributeSymbol = compilation.GetTypeByMetadataName(AttributeName);
            foreach (var candidateClass in receiver.CandidateClasses)
            {
                var model = compilation.GetSemanticModel(candidateClass.SyntaxTree);
                var typeSymbol = (INamedTypeSymbol) model.GetDeclaredSymbol(candidateClass);
                var relevantAttribute = typeSymbol.GetAttributes().FirstOrDefault(attr =>
                    attr.AttributeClass != null &&
                    attr.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default));

                if (relevantAttribute == null)
                {
                    continue;
                }

                var isPartial = candidateClass.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

                if (isPartial)
                {
                    symbols.Add(typeSymbol);
                }
                else
                {
                    var missingPartialKeywordMessage =
                        $"The type {typeSymbol.Name} should be declared with the 'partial' keyword " +
                        "as it is annotated with the [GenerateTypedNameReferences] attribute.";

                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "RXN0004",
                                missingPartialKeywordMessage,
                                missingPartialKeywordMessage,
                                "Usage",
                                DiagnosticSeverity.Error,
                                true),
                            Location.None));
                    return null;
                }
            }

            return symbols;
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new NameReferenceSyntaxReceiver());
        }
    }
}

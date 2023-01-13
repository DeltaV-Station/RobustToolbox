﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Robust.Shared.CompNetworkGenerator
{
    [Generator]
    public class ComponentNetworkGenerator : ISourceGenerator
    {
        private const string ClassAttributeName = "Robust.Shared.AutoGenerated.AutoGenerateComponentStateAttribute";
        private const string MemberAttributeName = "Robust.Shared.AutoGenerated.AutoNetworkedFieldAttribute";
        private const string AttributesFile = "ComponentNetworkGeneratorAttributes";

        private const string AttributesCode = @"// <auto-generated />
using System;
using JetBrains.Annotations;
using Robust.Shared.GameObjects;

namespace Robust.Shared.AutoGenerated;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[BaseTypeRequired(typeof(Component))]
public sealed class AutoGenerateComponentStateAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class AutoNetworkedFieldAttribute : Attribute { }
    ";

        private static string GenerateSource(INamedTypeSymbol classSymbol, CSharpCompilation comp)
        {
            var nameSpace = classSymbol.ContainingNamespace.ToDisplayString();
            var componentName = classSymbol.Name;
            var stateName = $"{componentName}_AutoState";


            var members = classSymbol.GetMembers();
            var fields = new List<(ITypeSymbol Type, string FieldName)>();
            var fieldAttr = comp.GetTypeByMetadataName(MemberAttributeName);

            foreach (var mem in members)
            {
                var attribute = mem.GetAttributes().FirstOrDefault(a =>
                    a.AttributeClass != null &&
                    a.AttributeClass.Equals(fieldAttr, SymbolEqualityComparer.Default));

                if (attribute == null)
                {
                    continue;
                }

                switch (mem)
                {
                    case IFieldSymbol field:
                        fields.Add((field.Type, field.Name));
                        break;
                    case IPropertySymbol prop:
                    {
                        if (prop.SetMethod == null || prop.SetMethod.DeclaredAccessibility != Accessibility.Public)
                        {
                            // todo exception
                        }

                        if (prop.GetMethod == null || prop.GetMethod.DeclaredAccessibility != Accessibility.Public)
                        {
                            // todo exception
                        }

                        fields.Add((prop.Type, prop.Name));
                        break;
                    }
                }
            }

            if (fields.Count == 0)
            {
                // todo exception
            }

            // eg:
            //     public string Name = default!;
            //     public int Count = default!;
            var stateFields = new StringBuilder();

            // eg:
            //             Name = component.Name,
            //             Count = component.Count,
            var getStateInit = new StringBuilder();

            // eg:
            //        component.Name = state.Name;
            //        component.Count = state.Count;
            var handleStateSetters = new StringBuilder();

            foreach (var (type, name) in fields)
            {
                stateFields.Append($@"
    public global::{type.Name} {name} = default!;");

                getStateInit.Append($@"
            {name} = component.{name},");

                handleStateSetters.Append($@"
        component.{name} = state.{name}");
            }

            return $@"// <auto-generated />
using Robust.Shared.GameStates;

namespace {nameSpace};

partial class {stateName} : ComponentState
{{
{stateFields}
}}

partial class {componentName}_AutoNetworkSystem : EntitySystem
{{
    public override Initialize()
    {{
        SubscribeLocalEvent<{componentName}, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<{componentName}, ComponentHandleState>(OnHandleState);
    }}

    private void OnGetState(EntityUid uid, {componentName} component, ref ComponentGetState args)
    {{
        args.State = new {stateName}
        {{
{getStateInit}
        }};
    }}

    private void OnHandleState(EntityUid uid, {componentName} component, ref ComponentHandleState args)
    {{
        if (args.Current is not {stateName} state)
            return;
{handleStateSetters}
    }}
}}
";
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // Add attribute source
            var comp = (CSharpCompilation) context.Compilation;
            if (comp.GetTypeByMetadataName(ClassAttributeName) == null || comp.GetTypeByMetadataName(MemberAttributeName) == null)
                context.AddSource(AttributesFile, SourceText.From(AttributesCode, Encoding.UTF8));
            var options = (CSharpParseOptions) comp.SyntaxTrees[0].Options;
            comp = comp.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(AttributesCode, Encoding.UTF8), options));
            if (!(context.SyntaxReceiver is NameReferenceSyntaxReceiver receiver))
            {
                return;
            }

            var symbols = GetAnnotatedTypes(context, comp, receiver);

            // Generate component sources and add

        }

        private IReadOnlyList<INamedTypeSymbol> GetAnnotatedTypes(in GeneratorExecutionContext context,
            CSharpCompilation comp, NameReferenceSyntaxReceiver receiver)
        {
            var symbols = new List<INamedTypeSymbol>();
            var attributeSymbol = comp.GetTypeByMetadataName(ClassAttributeName);
            foreach (var candidateClass in receiver.CandidateClasses)
            {
                var model = comp.GetSemanticModel(candidateClass.SyntaxTree);
                var typeSymbol = model.GetDeclaredSymbol(candidateClass);
                var relevantAttribute = typeSymbol?.GetAttributes().FirstOrDefault(attr =>
                    attr.AttributeClass != null &&
                    attr.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default));

                if (relevantAttribute == null)
                {
                    continue;
                }

                symbols.Add(typeSymbol);
            }

            return symbols;
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            if (!Debugger.IsAttached)
            {
                //Debugger.Launch();
            }
            context.RegisterForSyntaxNotifications(() => new NameReferenceSyntaxReceiver());
        }
    }
}

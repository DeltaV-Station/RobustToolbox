using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Robust.Shared.Generators
{
    [Generator]
    public class NetIDsGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            //no init
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var solutionPathFile =
                context.AdditionalFiles.FirstOrDefault(f => f.Path.EndsWith("SolutionPathForGenerator"));
            if (solutionPathFile == null)
            {
                var msg = "Unable to find SolutionPathForGenerator-File!";
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        new DiagnosticDescriptor("RNI0000",
                            msg,
                            msg, "MsBuild", DiagnosticSeverity.Error, true), Location.None));
                return;
            }

            var solutionPath = solutionPathFile.GetText()?.ToString();
            if (solutionPath == null)
            {
                var msg = "SolutionPathForGenerator-File was empty!";
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        new DiagnosticDescriptor("RNI0001",
                            msg,
                            msg, "MsBuild", DiagnosticSeverity.Error, true), Location.None));
                return;
            }

            var split = solutionPath.Split('/');
            solutionPath = string.Join("/", split.Take(split.Length - 1));

            bool TryGetProjectPath(string subdir, out string path)
            {
                path = $"{solutionPath}{subdir}";
                if (!Directory.Exists(path))
                {
                    var msg = $"Could not expected project at dir: {path}.";
                    context.ReportDiagnostic(
                        Diagnostic.Create(
                            new DiagnosticDescriptor("RNI0002",
                                msg,
                                msg, "MsBuild", DiagnosticSeverity.Warning, true), Location.None));
                    return false;
                }

                return true;
            }

            if (!TryGetProjectPath("/Content.Server", out var serverPath) ||
                !TryGetProjectPath("/Content.Shared", out var sharedPath) ||
                !TryGetProjectPath("/Content.Client", out var clientPath) ||
                !TryGetProjectPath("/RobustToolbox/Robust.Server", out var robustServerPath) ||
                !TryGetProjectPath("/RobustToolbox/Robust.Shared", out var robustSharedPath) ||
                !TryGetProjectPath("/RobustToolbox/Robust.Client", out var robustClientPath) ||
                !TryGetProjectPath("/Content.IntegrationTests", out var integrationTestsPath))
            {
                return;
            }


            var netIdWalker = new NetIDWalker(context);

            var sharedTrees = GetSyntaxTrees(sharedPath).Concat(GetSyntaxTrees(robustSharedPath)).ToArray();
            var sharedComp = CSharpCompilation.Create("shared", sharedTrees);
            netIdWalker.SetupComp(sharedComp);
            foreach (var syntaxTree in sharedTrees)
            {
                netIdWalker.Visit(syntaxTree.GetRoot());
            }


            var clientTrees = GetSyntaxTrees(clientPath).Concat(GetSyntaxTrees(robustClientPath)).ToArray();
            var clientComp = CSharpCompilation.Create("client", clientTrees);
            clientComp = clientComp.AddSyntaxTrees(sharedTrees);
            netIdWalker.SetupComp(clientComp);
            foreach (var syntaxTree in clientTrees)
            {
                netIdWalker.Visit(syntaxTree.GetRoot());
            }

            var serverTrees = GetSyntaxTrees(serverPath).Concat(GetSyntaxTrees(robustServerPath)).ToArray();
            var serverComp = CSharpCompilation.Create("server", serverTrees);
            serverComp = serverComp.AddSyntaxTrees(sharedTrees);
            netIdWalker.SetupComp(serverComp);
            foreach (var syntaxTree in serverTrees)
            {
                netIdWalker.Visit(syntaxTree.GetRoot());
            }

            var integrationTrees = GetSyntaxTrees(integrationTestsPath);
            var integrationComp = CSharpCompilation.Create("integrationTests", integrationTrees);
            integrationComp = integrationComp.AddSyntaxTrees(sharedTrees);
            integrationComp = integrationComp.AddSyntaxTrees(clientTrees);
            integrationComp = integrationComp.AddSyntaxTrees(serverTrees);
            netIdWalker.SetupComp(integrationComp);
            foreach (var syntaxTree in integrationTrees)
            {
                netIdWalker.Visit(syntaxTree.GetRoot());
            }

            string code = $@"
namespace Robust.Shared.AutoGenerated{{
    public static class NetIDs
    {{
";
            var i = 0;
            foreach (var entry in netIdWalker.NetIds)
            {
                code += $@"
        public const uint {entry.Key} = {i++};
";
            }
            code += @"
    }
}";
            context.AddSource("NetIDs.g.cs", SourceText.From(code, Encoding.UTF8));
        }

        static SyntaxTree[] GetSyntaxTrees(string projectpath)
        {
            return Directory
                .EnumerateFiles($"{projectpath}", "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.StartsWith($"{projectpath}/obj"))
                .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), CSharpParseOptions.Default)).ToArray();
        }

        class NetIDWalker : CSharpSyntaxWalker
        {
            public Dictionary<string, (INamedTypeSymbol, AttributeSyntax)> NetIds = new Dictionary<string, (INamedTypeSymbol, AttributeSyntax)>();
            private CSharpCompilation _comp;
            private GeneratorExecutionContext _context;

            public NetIDWalker(GeneratorExecutionContext context)
            {
                _context = context;
            }

            public void SetupComp(CSharpCompilation comp)
            {
                _comp = comp;
            }


            public override void VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                base.VisitClassDeclaration(node);
                var model = _comp.GetSemanticModel(node.SyntaxTree);
                var symbol = model.GetDeclaredSymbol(node);

                var attr = symbol?.GetAttributes().FirstOrDefault(a =>
                    a.ToString() == "Robust.Shared.Network.NetIDAttribute");

                /*_context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("RNI9999", $"{symbol.GetAttributes().FirstOrDefault()}",
                        $"{symbol.GetAttributes().FirstOrDefault()}", "Usage", DiagnosticSeverity.Error, true),
                    Location.None));*/
                if(attr == null) return;

                if (!(attr.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax attributeSyntax &&
                      attributeSyntax.ArgumentList?.Arguments.Count == 1 &&
                      attributeSyntax.ArgumentList?.Arguments[0].Expression is LiteralExpressionSyntax
                          literalExpressionSyntax)) return;

                var netId = literalExpressionSyntax.Token.Text.Replace("\"", "");

                if (NetIds.ContainsKey(netId))
                {
                    if(SymbolEqualityComparer.Default.Equals(NetIds[netId].Item1, symbol)) return;

                    var title = $"NetID '{netId}' defined twice!";
                    _context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("RNI0004", title, title, "Usage", DiagnosticSeverity.Error, true),
                        attributeSyntax.GetLocation()));
                    _context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("RNI0004", title, title, "Usage", DiagnosticSeverity.Error, true),
                        NetIds[netId].Item2.GetLocation()));
                    return;
                }

                if (string.IsNullOrWhiteSpace(netId))
                {
                    var title = $"Empty NetID found!";
                    _context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("RNI0005", title, title, "Usage", DiagnosticSeverity.Error, true),
                        node.GetLocation()));
                    return;
                }

                NetIds.Add(netId, (symbol, attributeSyntax));
            }
        }
    }
}

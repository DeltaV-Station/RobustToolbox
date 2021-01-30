using Microsoft.CodeAnalysis;

namespace Robust.Generators
{
    public static class Diagnostics
    {
        public static SuppressionDescriptor YamlMeansImplicitUse =>
            new SuppressionDescriptor("RADC1000", "CS0649", "Used by ComponentDataManager.");

        public static DiagnosticDescriptor InvalidYamlAttrTarget = new DiagnosticDescriptor(
            "RADC0000",
            "",
            $"YamlFieldAttribute assigned for Member which is neither Field or Property! It will be ignored.",
            "Usage",
            DiagnosticSeverity.Warning,
            true);

        public static DiagnosticDescriptor FailedCustomDataClassAttributeResolve = new DiagnosticDescriptor(
            "RADC0001",
            "",
            $"Could not resolve CustomDataClassAttribute",
            "Usage",
            DiagnosticSeverity.Error,
            true);

        public static DiagnosticDescriptor InvalidDeepCloneImpl = new DiagnosticDescriptor(
            "RADC0002",
            "",
            $"Invalid assignment found in DeepClone implementation",
            "Usage",
            DiagnosticSeverity.Error,
            true);

        public static DiagnosticDescriptor InvalidYamlTag = new DiagnosticDescriptor(
            "RADC0003",
            "",
            $"YamlFieldAttribute has an invalid tag.",
            "Usage",
            DiagnosticSeverity.Error,
            true);

        public static DiagnosticDescriptor NoDeepCloneImpl = new DiagnosticDescriptor(
            "RADC0004",
            "",
            $"Missing Implementation of IDeepClone.DeepClone",
            "Usage",
            DiagnosticSeverity.Error,
            true);
    }
}

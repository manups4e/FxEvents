using Microsoft.CodeAnalysis;

namespace FxEvents.SourceGenerator
{
    internal static class Diagnostics
    {
        public static readonly DiagnosticDescriptor MethodMustBeStatic = new DiagnosticDescriptor(
            id: "FXEV001",
            title: "Method must be static",
            messageFormat: "Method '{0}' is marked with [FxEvent] but is not static. FxEvents only supports static methods",
            category: "FxEvents",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor FromSourceMustBeFirst = new DiagnosticDescriptor(
            id: "FXEV002",
            title: "FromSource must be first parameter",
            messageFormat: "Method '{0}' has [FromSource] attribute on parameter '{1}', but [FromSource] must be placed on the first parameter",
            category: "FxEvents",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DuplicateWithReturnType = new DiagnosticDescriptor(
            id: "FXEV003",
            title: "Multiple handlers with return type",
            messageFormat: "Event '{0}' cannot have more than 1 handler with a return type",
            category: "FxEvents",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor MethodShouldNotBePrivate = new DiagnosticDescriptor(
            id: "FXEV004",
            title: "Method should be internal or public",
            messageFormat: "Method '{0}' is marked with [FxEvent] but is private. Make it internal or public so the Source Generator can bind to it directly",
            category: "FxEvents",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}

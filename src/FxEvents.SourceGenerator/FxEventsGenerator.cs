using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace FxEvents.SourceGenerator
{
    internal readonly struct MethodParseResult
    {
        public readonly ImmutableArray<EventMethodModel> Methods;
        public readonly ImmutableArray<Diagnostic> Diagnostics;

        public MethodParseResult(ImmutableArray<EventMethodModel> methods, ImmutableArray<Diagnostic> diagnostics)
        {
            Methods = methods;
            Diagnostics = diagnostics;
        }
    }

    [Generator(LanguageNames.CSharp)]
    public class FxEventsGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var methodDeclarations = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0,
                transform: static (ctx, _) => GetEventMethods(ctx)
            ).Where(static r => r.Methods.Length > 0 || r.Diagnostics.Length > 0);

            var collectedResults = methodDeclarations.Collect();

            context.RegisterSourceOutput(collectedResults, static (spc, results) =>
            {
                var allMethods = new List<EventMethodModel>();

                foreach (var result in results)
                {
                    foreach (var diag in result.Diagnostics)
                    {
                        spc.ReportDiagnostic(diag);
                    }

                    allMethods.AddRange(result.Methods);
                }

                if (allMethods.Count == 0) return;

                // Validate return types: max 1 per event name
                HashSet<string> withReturnType = new HashSet<string>();
                foreach (var method in allMethods)
                {
                    if (!method.ReturnsVoid)
                    {
                        if (withReturnType.Contains(method.EventName))
                        {
                            spc.ReportDiagnostic(Diagnostic.Create(
                                Diagnostics.DuplicateWithReturnType,
                                Location.None,
                                method.EventName));
                        }
                        else
                        {
                            withReturnType.Add(method.EventName);
                        }
                    }
                }

                string source = GenerateSource(allMethods);
                spc.AddSource("FxEventsGeneratedBindings.g.cs", SourceText.From(source, Encoding.UTF8));
            });
        }

        private static MethodParseResult GetEventMethods(GeneratorSyntaxContext context)
        {
            var methodSyntax = (MethodDeclarationSyntax)context.Node;
            if (context.SemanticModel.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol symbol)
                return default;

            var methods = ImmutableArray.CreateBuilder<EventMethodModel>();
            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

            foreach (var attr in symbol.GetAttributes())
            {
                string? attrName = attr.AttributeClass?.Name;
                if (attrName != "FxEvent" && attrName != "FxEventAttribute")
                    continue;

                // Check static
                if (!symbol.IsStatic)
                {
                    diagnostics.Add(Diagnostic.Create(
                        Diagnostics.MethodMustBeStatic,
                        methodSyntax.Identifier.GetLocation(),
                        symbol.Name));
                    continue;
                }

                // Check private accessibility warning
                if (symbol.DeclaredAccessibility == Accessibility.Private)
                {
                    diagnostics.Add(Diagnostic.Create(
                        Diagnostics.MethodShouldNotBePrivate,
                        methodSyntax.Identifier.GetLocation(),
                        symbol.Name));
                }

                // Check [FromSource] parameter placement
                bool hasFromSourceError = false;
                for (int i = 0; i < symbol.Parameters.Length; i++)
                {
                    var param = symbol.Parameters[i];
                    bool hasFromSource = param.GetAttributes().Any(a =>
                        a.AttributeClass?.Name == "FromSource" || a.AttributeClass?.Name == "FromSourceAttribute");

                    if (hasFromSource && i != 0)
                    {
                        hasFromSourceError = true;
                        var paramSyntax = methodSyntax.ParameterList.Parameters[i];
                        diagnostics.Add(Diagnostic.Create(
                            Diagnostics.FromSourceMustBeFirst,
                            paramSyntax.GetLocation(),
                            symbol.Name,
                            param.Name));
                    }
                }

                if (hasFromSourceError)
                    continue;

                // Extract event name (arg 0)
                string? eventName = null;
                if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string nameVal)
                {
                    eventName = nameVal;
                }
                else
                {
                    var named = attr.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Name");
                    if (named.Value.Value is string namedVal)
                        eventName = namedVal;
                }

                if (string.IsNullOrEmpty(eventName))
                    continue;

                // Extract binding (arg 1, default 3 = All)
                int binding = 3;
                if (attr.ConstructorArguments.Length > 1 && attr.ConstructorArguments[1].Value is int intVal)
                {
                    binding = intVal;
                }
                else
                {
                    var named = attr.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Binding");
                    if (named.Value.Value is int namedInt)
                        binding = namedInt;
                }

                string containingType = symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                string methodName = symbol.Name;
                bool returnsVoid = symbol.ReturnsVoid;
                string? returnType = returnsVoid ? null : symbol.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                string[] paramTypes = symbol.Parameters
                    .Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .ToArray();

                methods.Add(new EventMethodModel(
                    containingType,
                    methodName,
                    eventName!,
                    binding,
                    paramTypes,
                    returnType,
                    returnsVoid));
            }

            return new MethodParseResult(methods.ToImmutable(), diagnostics.ToImmutable());
        }

        private static string GenerateSource(List<EventMethodModel> methods)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("namespace FxEvents.Generated");
            sb.AppendLine("{");
            sb.AppendLine("    public static class FxEventsGeneratedBindings");
            sb.AppendLine("    {");
            sb.AppendLine("        public static void RegisterEvents()");
            sb.AppendLine("        {");

            foreach (var m in methods)
            {
                string target = $"{m.ContainingTypeFullyQualified}.{m.MethodName}";
                string delegateCreation;

                if (m.ReturnsVoid)
                {
                    if (m.ParameterTypes.Length == 0)
                    {
                        delegateCreation = $"new global::System.Action({target})";
                    }
                    else
                    {
                        string paramTypeList = string.Join(", ", m.ParameterTypes);
                        delegateCreation = $"new global::System.Action<{paramTypeList}>({target})";
                    }
                }
                else
                {
                    if (m.ParameterTypes.Length == 0)
                    {
                        delegateCreation = $"new global::System.Func<{m.ReturnType}>({target})";
                    }
                    else
                    {
                        string paramTypeList = string.Join(", ", m.ParameterTypes);
                        delegateCreation = $"new global::System.Func<{paramTypeList}, {m.ReturnType}>({target})";
                    }
                }

                sb.AppendLine($"            global::FxEvents.EventHub.Mount(\"{m.EventName}\", (global::FxEvents.Shared.Binding){m.BindingValue}, {delegateCreation});");
            }

            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}

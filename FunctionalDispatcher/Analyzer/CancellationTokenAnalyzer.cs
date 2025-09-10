using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SmallShopBigAmbitions.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CancellationTokenUsageAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "SBA0001";
    private static readonly LocalizableString Title = "CancellationToken is not used";
    private static readonly LocalizableString MessageFormat = "Handler '{0}' does not use its CancellationToken parameter";
    private static readonly LocalizableString Description = "Implementations of IFunctionalHandler<TRequest,TResponse>.Handle should observe or pass the CancellationToken.";
    private const string Category = "Reliability";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeNamedType, SymbolKind.NamedType);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context)
    {
        if (context.Symbol is not INamedTypeSymbol typeSymbol) return;

        var implementsHandler = typeSymbol.AllInterfaces.Any(i =>
            i.OriginalDefinition.ToDisplayString() == "SmallShopBigAmbitions.FunctionalDispatcher.IFunctionalHandler<TRequest, TResponse>");
        if (!implementsHandler) return;

        var handle = typeSymbol.GetMembers().OfType<IMethodSymbol>()
            .FirstOrDefault(m => m.Name == "Handle" && m.Parameters.Length == 3);
        if (handle is null) return;

        var ctParam = handle.Parameters.Last();
        if (ctParam.Type.ToDisplayString() != "System.Threading.CancellationToken") return;

        foreach (var declRef in handle.DeclaringSyntaxReferences)
        {
            if (declRef.GetSyntax() is not MethodDeclarationSyntax methodDecl) continue;
            var body = (SyntaxNode?)methodDecl.Body ?? methodDecl.ExpressionBody;
            if (body is null) continue;

            var ctName = ctParam.Name;
            var ctUsages = body.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Count(id => id.Identifier.Text == ctName);

            if (ctUsages == 0)
            {
                var diag = Diagnostic.Create(Rule, methodDecl.Identifier.GetLocation(), typeSymbol.Name);
                context.ReportDiagnostic(diag);
            }
        }
    }
}

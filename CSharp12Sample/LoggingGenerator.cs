using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Text;

[Generator(LanguageNames.CSharp)]
public class LoggingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var methodCalls = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is 
                InvocationExpressionSyntax 
                { 
                    Expression: MemberAccessExpressionSyntax 
                    { 
                        Name:
                        {
                            Identifier:
                            {
                                ValueText: "InterceptableMethod"
                            }
                        }
                    }
                }
            ,
            transform: static (context, token) =>
            {
                var operation = context.SemanticModel.GetOperation(context.Node, token);
                if (operation is IInvocationOperation targetOperation 
                    )
                {
                    return new InterceptInvocation(targetOperation);
                }
                return null;
            })
            .Where(static invocation => invocation != null);

        var interceptors = methodCalls.Collect()
            .Select((invocations, _) =>
            {
                var stringBuilder = new StringBuilder();
                foreach (var invocation in invocations)
                {
                    var definition = $$"""
                                               [System.Runtime.CompilerServices.InterceptsLocationAttribute(@"{{invocation.Location.FilePath}}", {{invocation.Location.Line}}, {{invocation.Location.Column}})]
                                               public static void LoggingInterceptorMethod(this CSharp12Sample.C c)
                                               {
                                                   System.Console.WriteLine("logging before...");
                                                   c.InterceptableMethod();
                                                   System.Console.WriteLine("logging after...");
                                               }
                                       """;
                    stringBuilder.Append(definition);
                    stringBuilder.AppendLine();
                }
                return stringBuilder.ToString();
            });

        context.RegisterSourceOutput(interceptors, (ctx, sources) =>
        {
            var code = $$"""
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    file sealed class InterceptsLocationAttribute(string filePath, int line, int character) : Attribute { }
}

namespace CSharp12Sample.Generated
{
    public static partial class GeneratedLogging
    {
{{sources}}
    }
}
""";
            ctx.AddSource("GeneratedLoggingInterceptor.g.cs", code);
        });
    }
}


file sealed class InterceptInvocation(IInvocationOperation invocationOperation)
{

    public (string FilePath, int Line, int Column) Location { get; } = GetLocation(invocationOperation);

    private static (string filePath, int line, int column) GetLocation(IInvocationOperation operation)
    {
        // The invocation expression consists of two properties:
        // - Expression: which is a `MemberAccessExpressionSyntax` that represents the method being invoked.
        // - ArgumentList: the list of arguments being invoked.
        // Here, we resolve the `MemberAccessExpressionSyntax` to get the location of the method being invoked.
        var memberAccessorExpression = ((MemberAccessExpressionSyntax)((InvocationExpressionSyntax)operation.Syntax).Expression);
        // The `MemberAccessExpressionSyntax` in turn includes three properties:
        // - Expression: the expression that is being accessed.
        // - OperatorToken: the operator token, typically the dot separate.
        // - Name: the name of the member being accessed, typically `MapGet` or `MapPost`, etc.
        // Here, we resolve the `Name` to extract the location of the method being invoked.
        var invocationNameSpan = memberAccessorExpression.Name.Span;
        // Resolve LineSpan associated with the name span so we can resolve the line and character number.
        var lineSpan = operation.Syntax.SyntaxTree.GetLineSpan(invocationNameSpan);
        // Resolve the filepath of the invocation while accounting for source mapped paths.
        var filePath = operation.Syntax.SyntaxTree.GetInterceptorFilePath(operation.SemanticModel?.Compilation.Options.SourceReferenceResolver);
        // LineSpan.LinePosition is 0-indexed, but we want to display 1-indexed line and character numbers in the interceptor attribute.
        return (filePath, lineSpan.StartLinePosition.Line + 1, lineSpan.StartLinePosition.Character + 1);
    }   
}

file static class Extensions
{
    
    // Utilize the same logic used by the interceptors API for resolving the source mapped
    // value of a path.
    // https://github.com/dotnet/roslyn/blob/f290437fcc75dad50a38c09e0977cce13a64f5ba/src/Compilers/CSharp/Portable/Compilation/CSharpCompilation.cs#L1063-L1064
    internal static string GetInterceptorFilePath(this SyntaxTree tree, SourceReferenceResolver? resolver) =>
        resolver?.NormalizePath(tree.FilePath, baseFilePath: null) ?? tree.FilePath;
}

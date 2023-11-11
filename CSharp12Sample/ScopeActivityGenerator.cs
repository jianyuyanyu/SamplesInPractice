﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Text;

namespace gen;

[Generator(LanguageNames.CSharp)]
public class ScopeActivityGenerator : IIncrementalGenerator
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
                                ValueText: "CreateScope"
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
                                               [System.Runtime.CompilerServices.InterceptsLocationAttribute(@"{{invocation!.Location.FilePath}}", {{invocation.Location.Line}}, {{invocation.Location.Column}})]
                                               public static Microsoft.Extensions.DependencyInjection.IServiceScope ScopeActivityInterceptorMethod(this System.IServiceProvider provider)
                                               {
                                                   System.Console.WriteLine("scope creating...");
                                                   var scope = provider.CreateScope();
                                                   var activity = scope.ServiceProvider.GetRequiredService<CSharp12Sample.ActivityScope>();
                                                   System.Console.WriteLine("scope created...");
                                                   return scope;
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
#pragma warning disable CS9113 // Parameter is unread.
    file sealed class InterceptsLocationAttribute(string filePath, int line, int character) : Attribute {}
#pragma warning restore CS9113 // Parameter is unread.
}

namespace CSharp12Sample.Generated
{
    public static partial class GeneratedActivityScope
    {
{{sources}}
    }
}
""";
            ctx.AddSource("GeneratedActivityScopeInterceptor.g.cs", code);
        });
    }
}

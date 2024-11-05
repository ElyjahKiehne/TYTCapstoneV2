using System.Text;
using Antlr4.Runtime.Misc;
using TYTCapstone;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class GroovyToCSharpTranspiler : GroovyParserBaseVisitor<string>
{
    private StringBuilder CodeBuilder = new StringBuilder();
    private int IndentLevel = 0;

    private CompilationUnitSyntax CsharpSyntaxTree = SyntaxFactory.CompilationUnit();

    // Entry point to generate C# code
    public string Transpile(GroovyParser.CompilationUnitContext context)
    {
        this.Visit(context);
        return CsharpSyntaxTree.ToFullString();
    }

    // Visit the compilation unit
    public override string VisitCompilationUnit([NotNull] GroovyParser.CompilationUnitContext context)
    {
        Console.WriteLine("Transpiling Compilation Unit");

        foreach (var child in context.children)
        {
            Console.WriteLine("DEBUG: child.Payload: " + child.Payload);
            Console.WriteLine("DEBUG: child.GetType(): " + child.GetType().ToString());
            // Visit(child);
        }
        return "";
    }

    // Visit class declaration
    public override string VisitClassDeclaration([NotNull] GroovyParser.ClassDeclarationContext context)
    {
        return "";
    }
}
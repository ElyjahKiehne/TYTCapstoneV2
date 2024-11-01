using System.Text;
using Antlr4.Runtime.Misc;
using TYTCapstone;

public class GroovyToCSharpTranspiler : GroovyParserBaseVisitor<string>
{
    private StringBuilder CodeBuilder = new StringBuilder();
    private int IndentLevel = 0;

    // Helper method to manage indentation
    private void AppendLine(string line)
    {
        CodeBuilder.AppendLine(new string(' ', IndentLevel * 4) + line);
    }

    // Entry point to generate C# code
    public string Transpile(GroovyParser.CompilationUnitContext context)
    {
        Visit(context);
        return CodeBuilder.ToString();
    }

    // Visit the compilation unit
    public override string VisitCompilationUnit(GroovyParser.CompilationUnitContext context)
    {
        foreach (var child in context.children)
        {
            Visit(child);
        }
        return null;
    }

    // Visit class declaration
    public override string VisitClassDeclaration(GroovyParser.ClassDeclarationContext context)
    {
        string className = context.IDENTIFIER().GetText();

        AppendLine($"public class {className}");
        AppendLine("{");
        IndentLevel++;

        // Visit class body
        Visit(context.classBody());

        IndentLevel--;
        AppendLine("}");
        return null;
    }
}
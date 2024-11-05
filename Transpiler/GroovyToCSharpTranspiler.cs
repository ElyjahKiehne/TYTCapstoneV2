using System.Text;
using Antlr4.Runtime.Misc;
using TYTCapstone;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Castle.DynamicProxy;
using static TYTCapstone.GroovyParser;

public class GroovyToCSharpTranspiler : GroovyParserBaseVisitor<string>
{
    private StringBuilder CodeBuilder = new StringBuilder();
    private int IndentLevel = 0;

    private CompilationUnitSyntax CsharpSyntaxTree = SyntaxFactory.CompilationUnit();

    // Entry point to generate C# code
    public string Transpile(GroovyParser.CompilationUnitContext context)
    {
        Visit(context);
        return CsharpSyntaxTree.ToFullString();
    }

    // Visit the compilation unit
    [AutoLog]
    public override string VisitCompilationUnit([NotNull] GroovyParser.CompilationUnitContext context)
    {
        Console.WriteLine("Transpiling Compilation Unit");

        foreach (var child in context.children)
        {
            Visit(child);
        }
        return "";
    }

    private SyntaxTokenList GetModifiers(GroovyParser.ClassDeclarationContext context)
    {
        var tokens = new List<SyntaxToken>();

        if (context.modifierSet != null)
        {
            foreach (var modifier in context.modifierSet)
            {
                switch (modifier)
                {
                    case "public":
                        tokens.Add(SyntaxFactory.Token(SyntaxKind.PublicKeyword));
                        break;
                    case "protected":
                        tokens.Add(SyntaxFactory.Token(SyntaxKind.ProtectedKeyword));
                        break;
                    case "private":
                        tokens.Add(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
                        break;
                    case "abstract":
                        tokens.Add(SyntaxFactory.Token(SyntaxKind.AbstractKeyword));
                        break;
                    case "static":
                        tokens.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));
                        break;
                    case "sealed":
                        tokens.Add(SyntaxFactory.Token(SyntaxKind.SealedKeyword));
                        break;
                        // Add other modifiers as needed
                }
            }
        }

        return SyntaxFactory.TokenList(tokens);
    }

    private BaseListSyntax GetBaseList(GroovyParser.ClassDeclarationContext context)
    {
        var baseTypes = new List<BaseTypeSyntax>();

        // Handle 'extends' clause
        if (context.extendsClause() != null)
        {
            foreach (var type in context.extendsClause().children)
            {
                var baseType = SyntaxFactory.SimpleBaseType(
                    SyntaxFactory.ParseTypeName(type.GetText()));
                baseTypes.Add(baseType);
            }
        }

        // Handle 'implements' clause if applicable
        if (context.implementsClause() != null)
        {
            foreach (var type in context.implementsClause().children)
            {
                var baseType = SyntaxFactory.SimpleBaseType(
                    SyntaxFactory.ParseTypeName(type.GetText()));
                baseTypes.Add(baseType);
            }
        }

        if (baseTypes.Count > 0)
        {
            return SyntaxFactory.BaseList(SyntaxFactory.SeparatedList(baseTypes));
        }

        return null;
    }

    // Visit class declaration
    [AutoLog]
    public override string VisitClassDeclaration([NotNull] GroovyParser.ClassDeclarationContext context)
    {
        Console.WriteLine("Transpiling Class Declaration");

        // Get the class name from the context
        string className = context.IDENTIFIER().GetText();

        MemberDeclarationSyntax classDeclaration;

        // Determine the type of class (class, interface, enum)
        if (context.isEnum != null)
        {
            classDeclaration = SyntaxFactory.EnumDeclaration(className);
        }
        else if (context.isInterface != null)
        {
            classDeclaration = SyntaxFactory.InterfaceDeclaration(className);
        }
        else
        {
            classDeclaration = SyntaxFactory.ClassDeclaration(className);
        }

        // Process modifiers
        var modifiers = GetModifiers(context);
        classDeclaration = classDeclaration.WithModifiers(modifiers);

        // Process base types (extends, implements)
        //var baseList = GetBaseList(context);
        //if (baseList != null)
        //{
        //    classDeclaration = classDeclaration.WithBaseList(baseList);
        //}

        //// Collect members
        //var members = new List<MemberDeclarationSyntax>();
        //if (context.classBody() != null)
        //{
        //    foreach (var memberContext in context.classBody().children)
        //    {
        //        var member = Visit(memberContext);
        //        if (member is MemberDeclarationSyntax memberDeclaration)
        //        {
        //            members.Add(memberDeclaration);
        //        }
        //    }
        //}

        //classDeclaration = classDeclaration.With(SyntaxFactory.List(members));

        return "";
    }

    [AutoLog]
    public override string VisitMethodDeclaration([NotNull] GroovyParser.MethodDeclarationContext context)
    {
        return "";
    }

    [AutoLog]
    public override string VisitFieldDeclaration([NotNull] GroovyParser.FieldDeclarationContext context)
    {
        return "";
    }

    [AutoLog]
    public override string VisitExpressionStatement([NotNull] GroovyParser.ExpressionStatementContext context)
    {
        return "";
    }

    [AutoLog]
    public override string VisitBlockStatement([NotNull] GroovyParser.BlockStatementContext context)
    {
        return "";
    }
}
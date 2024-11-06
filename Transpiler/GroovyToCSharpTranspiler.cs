using System.Text;
using Antlr4.Runtime.Misc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace TYTCapstone.Transpiler
{
public class GroovyToCSharpTranspiler : GroovyParserBaseVisitor<CSharpSyntaxNode>
{
    private readonly List<UsingDirectiveSyntax> Usings = new();
    private readonly List<MemberDeclarationSyntax> Members = new();

    public CompilationUnitSyntax Transpile(GroovyParser.CompilationUnitContext context)
    {
        return (CompilationUnitSyntax)Visit(context);
    }

    public override CSharpSyntaxNode VisitCompilationUnit([NotNull] GroovyParser.CompilationUnitContext context)
    {
        var statements = new List<StatementSyntax>();
        
        // Handle each scriptPart, which can be separated by newlines or semicolons
        foreach (var scriptPart in context.scriptPart())
        {
            var visitedPart = Visit(scriptPart);
            if (visitedPart is StatementSyntax statementSyntax)
            {
                statements.Add(statementSyntax);
            }
        }

        // Create the compilation unit with the namespace and class
        return CompilationUnit()
            .WithUsings(SingletonList(
                UsingDirective(
                    IdentifierName("System"))))
            .WithMembers(
                SingletonList<MemberDeclarationSyntax>(
                    NamespaceDeclaration(
                        IdentifierName("GroovyTranspiled"))
                    .WithMembers(
                        SingletonList<MemberDeclarationSyntax>(
                            ClassDeclaration("Program")
                            .WithModifiers(
                                TokenList(
                                    Token(SyntaxKind.PublicKeyword)))
                            .WithMembers(
                                SingletonList<MemberDeclarationSyntax>(
                                    MethodDeclaration(
                                        PredefinedType(
                                            Token(SyntaxKind.VoidKeyword)),
                                        Identifier("Main"))
                                    .WithModifiers(
                                        TokenList(
                                            Token(SyntaxKind.PublicKeyword),
                                            Token(SyntaxKind.StaticKeyword)))
                                    .WithBody(Block(statements))
                                )
                            )
                        )
                    )
                )
            );
    }

    public override CSharpSyntaxNode VisitScriptPart([NotNull] GroovyParser.ScriptPartContext context)
    {
        if (context.statement() != null)
        {
            return Visit(context.statement());
        }
        else if (context.methodDeclaration() != null)
        {
            return Visit(context.methodDeclaration());
        }
        return null;
    }

    public override CSharpSyntaxNode VisitPackageDefinition([NotNull] GroovyParser.PackageDefinitionContext context)
    {
        if (context == null) return null!;
        // From grammar: KW_PACKAGE (IDENTIFIER (DOT IDENTIFIER)*)
        var packagePath = string.Join(".", 
            context.IDENTIFIER().Select(id => id.GetText()));

        // Convert package to namespace
        Members.Add(
            NamespaceDeclaration(ParseName(packagePath))
        );

        return null;
    }

    public override CSharpSyntaxNode VisitImportStatement([NotNull] GroovyParser.ImportStatementContext context)
    {
        if (context == null) return null!;
        // From grammar: KW_IMPORT KW_STATIC? (IDENTIFIER (DOT IDENTIFIER)* (DOT MULT)?)
        var importPath = string.Join(".",
            context.IDENTIFIER().Select(id => id.GetText()));

        // Add import as using directive
        Usings.Add(
            UsingDirective(
                ParseName(importPath)
            )
        );

        return null;
    }

    public override CSharpSyntaxNode VisitClassDeclaration([NotNull] GroovyParser.ClassDeclarationContext context)
    {
        var modifiers = new List<SyntaxKind>();
        var className = context.IDENTIFIER().GetText();

        // Process class modifiers
        foreach (var modifier in context.classModifier())
        {
            switch (modifier.GetText())
            {
                case "public": modifiers.Add(SyntaxKind.PublicKeyword); break;
                case "private": modifiers.Add(SyntaxKind.PrivateKeyword); break;
                case "protected": modifiers.Add(SyntaxKind.ProtectedKeyword); break;
                case "static": modifiers.Add(SyntaxKind.StaticKeyword); break;
                case "abstract": modifiers.Add(SyntaxKind.AbstractKeyword); break;
                case "final": modifiers.Add(SyntaxKind.SealedKeyword); break;
            }
        }

        // Create class declaration
        var classDecl = ClassDeclaration(className)
            .WithModifiers(TokenList(modifiers.Select(m => Token(m))));

        // Process extends clause
        if (context.extendsClause() != null)
        {
            var baseTypes = new List<BaseTypeSyntax>();
            foreach (var baseType in context.extendsClause().genericClassNameExpression())
            {
                baseTypes.Add(
                    SimpleBaseType(
                        ParseTypeName(baseType.GetText())
                    )
                );
            }
            classDecl = classDecl.WithBaseList(
                BaseList(SeparatedList(baseTypes))
            );
        }

        // Process implements clause
        if (context.implementsClause() != null)
        {
            var interfaces = context.implementsClause().genericClassNameExpression()
                .Select(i => SimpleBaseType(ParseTypeName(i.GetText())));
            
            var baseList = classDecl.BaseList ?? BaseList();
            classDecl = classDecl.WithBaseList(
                baseList.WithTypes(
                    baseList.Types.AddRange(interfaces)
                )
            );
        }

        // Process class body
        if (context.classBody() != null)
        {
            var members = new List<MemberDeclarationSyntax>();
            foreach (var member in context.classBody().classMember())
            {
                var memberNode = Visit(member);
                if (memberNode is MemberDeclarationSyntax memberDecl)
                {
                    members.Add(memberDecl);
                }
            }
            classDecl = classDecl.WithMembers(List(members));
        }

        return classDecl;
    }

    public override CSharpSyntaxNode VisitFieldDeclaration([NotNull] GroovyParser.FieldDeclarationContext context)
    {
        var declarations = context.singleDeclaration();
        var firstDeclaration = declarations[0];
        var identifier = firstDeclaration.IDENTIFIER().GetText();
        var expression = firstDeclaration.expression();

        var variableDeclaration = VariableDeclaration(
            IdentifierName("var"))
            .WithVariables(
                SingletonSeparatedList(
                    VariableDeclarator(
                        Identifier(identifier))
                    .WithInitializer(
                        EqualsValueClause(
                            (ExpressionSyntax)Visit(expression)))));

        return LocalDeclarationStatement(variableDeclaration);
    }

    public override CSharpSyntaxNode VisitExpressionStatement([NotNull] GroovyParser.ExpressionStatementContext context)
    {
        var expr = context.expression();
        if (expr.GetText().StartsWith("println"))
        {
            // Handle println statement
            return ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("Console"),
                        IdentifierName("WriteLine")))
                .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(
                                (ExpressionSyntax)Visit(expr))))));
        }
        return ExpressionStatement((ExpressionSyntax)Visit(expr));
    }

    public override CSharpSyntaxNode VisitBlockStatement([NotNull] GroovyParser.BlockStatementContext context)
    {
        var statements = new List<StatementSyntax>();
        
        foreach (var stmt in context.statement())
        {
            var visitedStmt = Visit(stmt);
            if (visitedStmt is StatementSyntax statementSyntax)
            {
                statements.Add(statementSyntax);
            }
        }
        
        return Block(statements);
    }

    public override CSharpSyntaxNode VisitDeclarationStatement([NotNull] GroovyParser.DeclarationStatementContext context)
    {
        return Visit(context.declarationRule());
    }

    public override CSharpSyntaxNode VisitConstantExpression([NotNull] GroovyParser.ConstantExpressionContext context)
    {
        if (context.STRING() != null)
        {
            var str = context.STRING().GetText();
            // Remove the single quotes and add double quotes
            str = "\"" + str.Substring(1, str.Length - 2) + "\"";
            return LiteralExpression(
                SyntaxKind.StringLiteralExpression,
                Literal(str));
        }
        return base.VisitConstantExpression(context);
    }

    public override CSharpSyntaxNode VisitVariableExpression([NotNull] GroovyParser.VariableExpressionContext context)
    {
        return IdentifierName(context.IDENTIFIER().GetText());
    }
} // Close GroovyToCSharpTranspiler class
} // Close TYTCapstone.Transpiler namespace
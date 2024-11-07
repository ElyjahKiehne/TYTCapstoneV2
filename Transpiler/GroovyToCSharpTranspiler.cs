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
        
        foreach (var scriptPart in context.scriptPart())
        {
            var visitedPart = Visit(scriptPart);
            if (visitedPart is StatementSyntax statementSyntax)
            {
                statements.Add(statementSyntax);
            }
        }

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
        Console.WriteLine($"Visiting ExpressionStatement: {context.GetText()}");
        
        var expressionNode = Visit(context.expression());
        
        if (expressionNode == null)
        {
            throw new InvalidOperationException($"Failed to parse expression: {context.expression().GetText()}");
        }

        if (expressionNode is not ExpressionSyntax expr)
        {
            throw new InvalidOperationException($"Expected ExpressionSyntax but got {expressionNode.GetType().Name}");
        }

        return ExpressionStatement(expr);
    }

    public override CSharpSyntaxNode VisitCmdExpression([NotNull] GroovyParser.CmdExpressionContext context)
    {
        Console.WriteLine($"Visiting CmdExpression: {context.GetText()}");
        
        var nonKwCalls = context.nonKwCallExpressionRule();
        if (nonKwCalls != null && nonKwCalls.Length > 0)
        {
            return Visit(nonKwCalls[0]);
        }
        
        return base.VisitCmdExpression(context);
    }

    public override CSharpSyntaxNode VisitNonKwCallExpressionRule([NotNull] GroovyParser.NonKwCallExpressionRuleContext context)
    {
        Console.WriteLine($"Visiting NonKwCallExpressionRule: {context.GetText()}");
        
        if (context.IDENTIFIER() != null && context.argumentList() != null)
        {
            var methodName = context.IDENTIFIER().GetText();
            var argumentList = context.argumentList();
            
            var argumentListNode = Visit(argumentList);
            
            if (argumentListNode is ArgumentListSyntax args)
            {
                if (methodName == "println")
                {
                    return InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("Console"),
                            IdentifierName("WriteLine")),
                        args);
                }
                
                return InvocationExpression(
                    IdentifierName(methodName),
                    args);
            }
            else
            {
                throw new InvalidOperationException($"Expected ArgumentListSyntax but got {argumentListNode?.GetType().Name ?? "null"}");
            }
        }
        
        return base.VisitNonKwCallExpressionRule(context);
    }

    public override CSharpSyntaxNode VisitArgumentList([NotNull] GroovyParser.ArgumentListContext context)
    {
        Console.WriteLine($"Visiting ArgumentList: {context.GetText()}");
        
        var arguments = new List<ArgumentSyntax>();
        
        foreach (var arg in context.argument())
        {
            var argNode = Visit(arg);
            if (argNode is ExpressionSyntax expr)
            {
                arguments.Add(Argument(expr));
            }
            else
            {
                throw new InvalidOperationException($"Expected ExpressionSyntax but got {argNode?.GetType().Name ?? "null"}");
            }
        }
        
        return ArgumentList(SeparatedList<ArgumentSyntax>(arguments));
    }

    public override CSharpSyntaxNode VisitArgument([NotNull] GroovyParser.ArgumentContext context)
    {
        Console.WriteLine($"Visiting Argument: {context.GetText()}");
        return Visit(context.expression());
    }

    public override CSharpSyntaxNode VisitConstantExpression([NotNull] GroovyParser.ConstantExpressionContext context)
    {
        var text = context.STRING().GetText();
        // Remove the surrounding quotes (either single or double)
        text = text.Substring(1, text.Length - 2);
        return LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            Literal(text));
    }

    public override CSharpSyntaxNode VisitGstringExpression([NotNull] GroovyParser.GstringExpressionContext context)
    {
        Console.WriteLine($"Visiting GstringExpression: {context.GetText()}");
        
        var parts = new List<InterpolatedStringContentSyntax>();
        var gstring = context.gstring();
        
        // Handle the start text before the first interpolation
        var startText = gstring.GSTRING_START().GetText();
        startText = startText.Substring(2); // Remove the "$ part
        if (!string.IsNullOrEmpty(startText))
        {
            parts.Add(InterpolatedStringText(
                Token(TriviaList(), SyntaxKind.InterpolatedStringTextToken, startText, startText, TriviaList())
            ));
        }

        // Handle each expression body and the text parts that follow
        for (int i = 0; i < gstring.gstringExpressionBody().Length; i++)
        {
            var body = gstring.gstringExpressionBody()[i];
            var expr = Visit(body);
            if (expr is ExpressionSyntax exprSyntax)
            {
                parts.Add(Interpolation(exprSyntax));
            }

            // Add the text part that follows (if any)
            if (i < gstring.GSTRING_PART().Length)
            {
                var text = gstring.GSTRING_PART()[i].GetText();
                text = text.Substring(0, text.Length - 1); // Remove the $ part
                //text = text.Substring(2); // Remove the ", $" part
                if (!string.IsNullOrEmpty(text))
                {
                    parts.Add(InterpolatedStringText(
                        Token(TriviaList(), SyntaxKind.InterpolatedStringTextToken, text, text, TriviaList())
                    ));
                }
            }
        }

        // Handle the end text
        var endText = gstring.GSTRING_END().GetText();
        endText = endText.Substring(0, endText.Length - 1); // Remove the closing quote
        if (!string.IsNullOrEmpty(endText))
        {
            parts.Add(InterpolatedStringText(
                Token(TriviaList(), SyntaxKind.InterpolatedStringTextToken, endText, endText, TriviaList())
            ));
        }

        return InterpolatedStringExpression(
            Token(SyntaxKind.InterpolatedStringStartToken),
            List(parts),
            Token(SyntaxKind.InterpolatedStringEndToken));
    }

    public override CSharpSyntaxNode VisitGstringExpressionBody([NotNull] GroovyParser.GstringExpressionBodyContext context)
    {
        Console.WriteLine($"Visiting GstringExpressionBody: {context.GetText()}");
        
        if (context.expression() != null)
        {
            return Visit(context.expression());
        }
        
        return base.VisitGstringExpressionBody(context);
    }

    public override CSharpSyntaxNode VisitVariableExpression([NotNull] GroovyParser.VariableExpressionContext context)
    {
        Console.WriteLine($"Visiting VariableExpression: {context.GetText()}");
        return IdentifierName(context.IDENTIFIER().GetText());
    }
} // Close GroovyToCSharpTranspiler class
} // Close TYTCapstone.Transpiler namespace

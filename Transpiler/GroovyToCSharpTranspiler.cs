using System.Text;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
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

    /// <summary>
    /// Change how much logging information is seen when the autologger 
    /// intercepts a visitor and displays its information.
    /// </summary>
    private const LoggerVerbosity LoggingVerbosity = LoggerVerbosity.Verbose;

    public CompilationUnitSyntax Transpile(GroovyParser.CompilationUnitContext context)
    {
        return (CompilationUnitSyntax)Visit(context);
    }

    [AutoLog(LoggingVerbosity)]
    public override CSharpSyntaxNode VisitCompilationUnit([NotNull] GroovyParser.CompilationUnitContext context)
    {
        var usingDirectives = new List<UsingDirectiveSyntax>();
        var members = new List<MemberDeclarationSyntax>();

        foreach (IParseTree child in context.children)
        {
            // Visit each child and determine the type of node returned
            var visitedNode = Visit(child);

            if (visitedNode is UsingDirectiveSyntax usingDirective)
            {
                // Collect using directives separately
                usingDirectives.Add(usingDirective);
            }
            else if (visitedNode is MemberDeclarationSyntax member)
            {
                // Collect top-level members like classes, interfaces, enums
                members.Add(member);
            }
            else if (visitedNode is StatementSyntax statement)
            {
                // Wrap top-level statements in GlobalStatementSyntax
                var globalStatement = SyntaxFactory.GlobalStatement(statement);
                members.Add(globalStatement);
            }
        }

        // Create the compilation unit, adding both usings and members
        return SyntaxFactory.CompilationUnit()
            .AddUsings(usingDirectives.ToArray())
            .AddMembers(members.ToArray())
            .NormalizeWhitespace();
    }


    [AutoLog(LoggingVerbosity)]
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

    [AutoLog(LoggingVerbosity)]
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

    [AutoLog(LoggingVerbosity)]
    public override CSharpSyntaxNode VisitExpressionStatement([NotNull] GroovyParser.ExpressionStatementContext context)
    {
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

    [AutoLog(LoggingVerbosity)]
    public override CSharpSyntaxNode VisitCmdExpression([NotNull] GroovyParser.CmdExpressionContext context)
    {
        var nonKwCalls = context.nonKwCallExpressionRule();
        if (nonKwCalls != null && nonKwCalls.Length > 0)
        {
            return Visit(nonKwCalls[0]);
        }
        
        return base.VisitCmdExpression(context);
    }

    [AutoLog(LoggingVerbosity)]
    public override CSharpSyntaxNode VisitNonKwCallExpressionRule([NotNull] GroovyParser.NonKwCallExpressionRuleContext context)
    {
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

    [AutoLog(LoggingVerbosity)]
    public override CSharpSyntaxNode VisitArgumentList([NotNull] GroovyParser.ArgumentListContext context)
    {
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

    [AutoLog(LoggingVerbosity)]
    public override CSharpSyntaxNode VisitArgument([NotNull] GroovyParser.ArgumentContext context)
    {
        return Visit(context.expression());
    }

    [AutoLog(LoggingVerbosity)]
    public override CSharpSyntaxNode VisitConstantExpression([NotNull] GroovyParser.ConstantExpressionContext context)
    {
        var text = context.STRING().GetText();
        // Remove the surrounding quotes (either single or double)
        text = text.Substring(1, text.Length - 2);
        return LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            Literal(text));
    }

    [AutoLog(LoggingVerbosity)]
    public override CSharpSyntaxNode VisitGstringExpression([NotNull] GroovyParser.GstringExpressionContext context)
    {
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

    [AutoLog(LoggingVerbosity)]
    public override CSharpSyntaxNode VisitGstringExpressionBody([NotNull] GroovyParser.GstringExpressionBodyContext context)
    {
        if (context.expression() != null)
        {
            return Visit(context.expression());
        }
        
        return base.VisitGstringExpressionBody(context);
    }

    [AutoLog(LoggingVerbosity)]
    public override CSharpSyntaxNode VisitVariableExpression([NotNull] GroovyParser.VariableExpressionContext context)
    {
        return IdentifierName(context.IDENTIFIER().GetText());
    }
} // Close GroovyToCSharpTranspiler class
} // Close TYTCapstone.Transpiler namespace

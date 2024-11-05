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
        private int IndentLevel = 0;
        private CompilationUnitSyntax CurrentCompilationUnit;
        private readonly List<UsingDirectiveSyntax> Usings = new();
        private readonly List<MemberDeclarationSyntax> Members = new();

        public CompilationUnitSyntax Transpile(GroovyParser.CompilationUnitContext context)
        {
            return (CompilationUnitSyntax)Visit(context);
        }

        public override CSharpSyntaxNode VisitCompilationUnit([NotNull] GroovyParser.CompilationUnitContext context)
        {
            // Process package definition if exists
            if (context.packageDefinition() != null)
            {
                Visit(context.packageDefinition());
            }

            // Process imports
            foreach (var import in context.importStatement())
            {
                Visit(import);
            }

            // Process all class declarations and other top-level elements
            foreach (var member in context.children)
            {
                var result = Visit(member);
                if (result is MemberDeclarationSyntax memberDecl)
                {
                    Members.Add(memberDecl);
                }
            }

            // Build the compilation unit
            return CompilationUnit()
                .WithUsings(List(Usings))
                .WithMembers(List(Members));
        }

        public override CSharpSyntaxNode VisitPackageDefinition([NotNull] GroovyParser.PackageDefinitionContext context)
        {
            // Convert Groovy package to C# namespace
            var namespaceName = context.GetText().Replace("package", "").Trim();
            return NamespaceDeclaration(ParseName(namespaceName));
        }

        public override CSharpSyntaxNode VisitImportStatement([NotNull] GroovyParser.ImportStatementContext context)
        {
            var importPath = context.GetText().Replace("import", "").Trim();
            Usings.Add(UsingDirective(ParseName(importPath)));
            return null;
        }

        public override CSharpSyntaxNode VisitClassDeclaration([NotNull] GroovyParser.ClassDeclarationContext context)
        {
            var modifiers = new List<SyntaxKind>();
            var className = context.className.Text;

            // Process class modifiers
            foreach (var modifier in context.classModifier())
            {
                var modifierText = modifier.GetText();
                switch (modifierText)
                {
                    case "public": modifiers.Add(SyntaxKind.PublicKeyword); break;
                    case "private": modifiers.Add(SyntaxKind.PrivateKeyword); break;
                    case "protected": modifiers.Add(SyntaxKind.ProtectedKeyword); break;
                    case "static": modifiers.Add(SyntaxKind.StaticKeyword); break;
                    case "abstract": modifiers.Add(SyntaxKind.AbstractKeyword); break;
                }
            }

            // Create the class declaration
            var classDecl = ClassDeclaration(className)
                .WithModifiers(TokenList(modifiers.Select(m => Token(m))));

            // Process extends clause
            if (context.extendsClause() != null)
            {
                var baseType = Visit(context.extendsClause());
                if (baseType is TypeSyntax baseTypeSyntax)
                {
                    classDecl = classDecl.WithBaseList(
                        BaseList(SingletonSeparatedList<BaseTypeSyntax>(
                            SimpleBaseType(baseTypeSyntax))));
                }
            }

            // Process implements clause
            if (context.implementsClause() != null)
            {
                var interfaces = Visit(context.implementsClause());
                // Add interfaces to base list...
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

        public override CSharpSyntaxNode VisitMethodDeclaration([NotNull] GroovyParser.MethodDeclarationContext context)
        {
            var modifiers = new List<SyntaxKind>();
            var methodName = context.methodName.Text;
            
            // Process method modifiers
            foreach (var modifier in context.memberModifier())
            {
                var modifierText = modifier.GetText();
                switch (modifierText)
                {
                    case "public": modifiers.Add(SyntaxKind.PublicKeyword); break;
                    case "private": modifiers.Add(SyntaxKind.PrivateKeyword); break;
                    case "protected": modifiers.Add(SyntaxKind.ProtectedKeyword); break;
                    case "static": modifiers.Add(SyntaxKind.StaticKeyword); break;
                    case "abstract": modifiers.Add(SyntaxKind.AbstractKeyword); break;
                }
            }

            // Get return type
            TypeSyntax returnType = PredefinedType(Token(SyntaxKind.VoidKeyword)); // Default to void
            if (context.returnType != null)
            {
                returnType = ParseTypeName(context.returnType.Text);
            }

            // Process parameters
            var parameters = new List<ParameterSyntax>();
            if (context.argumentDeclarationList() != null)
            {
                foreach (var param in context.argumentDeclarationList().argumentDeclaration())
                {
                    parameters.Add(
                        Parameter(Identifier(param.parameterName.Text))
                            .WithType(ParseTypeName(param.parameterType.Text)));
                }
            }

            // Create method declaration
            var methodDecl = MethodDeclaration(returnType, methodName)
                .WithModifiers(TokenList(modifiers.Select(m => Token(m))))
                .WithParameterList(ParameterList(SeparatedList(parameters)));

            // Process method body
            if (context.methodBody != null)
            {
                var bodyNode = Visit(context.methodBody);
                if (bodyNode is BlockSyntax block)
                {
                    methodDecl = methodDecl.WithBody(block);
                }
            }

            return methodDecl;
        }

        public override CSharpSyntaxNode VisitFieldDeclaration([NotNull] GroovyParser.FieldDeclarationContext context)
        {
            var modifiers = new List<SyntaxKind>();
            
            // Process field modifiers
            foreach (var modifier in context.memberModifier())
            {
                var modifierText = modifier.GetText();
                switch (modifierText)
                {
                    case "public": modifiers.Add(SyntaxKind.PublicKeyword); break;
                    case "private": modifiers.Add(SyntaxKind.PrivateKeyword); break;
                    case "protected": modifiers.Add(SyntaxKind.ProtectedKeyword); break;
                    case "static": modifiers.Add(SyntaxKind.StaticKeyword); break;
                    case "final": modifiers.Add(SyntaxKind.ReadOnlyKeyword); break;
                }
            }

            // Get field type and name
            var fieldType = ParseTypeName(context.fieldType.Text);
            var fieldName = context.fieldName.Text;

            // Create field declaration
            var variableDeclaration = VariableDeclaration(fieldType)
                .WithVariables(SingletonSeparatedList(
                    VariableDeclarator(fieldName)));

            return FieldDeclaration(variableDeclaration)
                .WithModifiers(TokenList(modifiers.Select(m => Token(m))));
        }

        public override CSharpSyntaxNode VisitBlockStatement([NotNull] GroovyParser.BlockStatementContext context)
        {
            var statements = new List<StatementSyntax>();

            foreach (var stmt in context.statement())
            {
                var stmtNode = Visit(stmt);
                if (stmtNode is StatementSyntax stmtSyntax)
                {
                    statements.Add(stmtSyntax);
                }
            }

            return Block(List(statements));
        }

        public override CSharpSyntaxNode VisitExpressionStatement([NotNull] GroovyParser.ExpressionStatementContext context)
        {
            var expr = Visit(context.expression());
            if (expr is ExpressionSyntax exprSyntax)
            {
                return ExpressionStatement(exprSyntax);
            }
            return null;
        }

        public override CSharpSyntaxNode VisitVariableExpression([NotNull] GroovyParser.VariableExpressionContext context)
        {
            return IdentifierName(context.GetText());
        }

        public override CSharpSyntaxNode VisitBinaryExpression([NotNull] GroovyParser.BinaryExpressionContext context)
        {
            var left = Visit(context.left);
            var right = Visit(context.right);
            var op = context.op.Text;

            if (left is ExpressionSyntax leftExpr && right is ExpressionSyntax rightExpr)
            {
                SyntaxKind kind = op switch
                {
                    "+" => SyntaxKind.AddExpression,
                    "-" => SyntaxKind.SubtractExpression,
                    "*" => SyntaxKind.MultiplyExpression,
                    "/" => SyntaxKind.DivideExpression,
                    "==" => SyntaxKind.EqualsExpression,
                    "!=" => SyntaxKind.NotEqualsExpression,
                    "<" => SyntaxKind.LessThanExpression,
                    ">" => SyntaxKind.GreaterThanExpression,
                    "<=" => SyntaxKind.LessThanOrEqualExpression,
                    ">=" => SyntaxKind.GreaterThanOrEqualExpression,
                    "&&" => SyntaxKind.LogicalAndExpression,
                    "||" => SyntaxKind.LogicalOrExpression,
                    _ => SyntaxKind.None
                };

                return BinaryExpression(kind, leftExpr, rightExpr);
            }

            return null;
        }

        public override CSharpSyntaxNode VisitCallExpression([NotNull] GroovyParser.CallExpressionContext context)
        {
            var method = Visit(context.method);
            var args = new List<ArgumentSyntax>();

            if (context.argumentList() != null)
            {
                foreach (var arg in context.argumentList().argument())
                {
                    var argNode = Visit(arg);
                    if (argNode is ExpressionSyntax argExpr)
                    {
                        args.Add(Argument(argExpr));
                    }
                }
            }

            if (method is ExpressionSyntax methodExpr)
            {
                return InvocationExpression(methodExpr)
                    .WithArgumentList(ArgumentList(SeparatedList(args)));
            }

            return null;
        }

        public override CSharpSyntaxNode VisitIfStatement([NotNull] GroovyParser.IfStatementContext context)
        {
            var condition = Visit(context.expression());
            var thenStatement = Visit(context.tb);
            
            if (condition is ExpressionSyntax conditionExpr && thenStatement is StatementSyntax thenStmt)
            {
                var ifStatement = IfStatement(conditionExpr, thenStmt);
                
                // Handle else clause if present
                if (context.eb != null)
                {
                    var elseStatement = Visit(context.eb);
                    if (elseStatement is StatementSyntax elseStmt)
                    {
                        ifStatement = ifStatement.WithElse(ElseClause(elseStmt));
                    }
                }
                
                return ifStatement;
            }
            return null;
        }

        public override CSharpSyntaxNode VisitWhileStatement([NotNull] GroovyParser.WhileStatementContext context)
        {
            var condition = Visit(context.expression());
            var body = Visit(context.tb);
            
            if (condition is ExpressionSyntax conditionExpr && body is StatementSyntax bodyStmt)
            {
                return WhileStatement(conditionExpr, bodyStmt);
            }
            return null;
        }

        public override CSharpSyntaxNode VisitForInStatement([NotNull] GroovyParser.ForInStatementContext context)
        {
            // Convert Groovy for-in to C# foreach
            var variable = Visit(context.variable);
            var collection = Visit(context.collection);
            var body = Visit(context.body);

            if (variable is ExpressionSyntax varExpr && 
                collection is ExpressionSyntax collExpr && 
                body is StatementSyntax bodyStmt)
            {
                return ForEachStatement(
                    Declaration(ParseTypeName("var"), Identifier(varExpr.ToString())),
                    collExpr,
                    bodyStmt
                );
            }
            return null;
        }

        public override CSharpSyntaxNode VisitClassicForStatement([NotNull] GroovyParser.ClassicForStatementContext context)
        {
            var initialization = Visit(context.init);
            var condition = Visit(context.condition);
            var update = Visit(context.update);
            var body = Visit(context.body);

            if (initialization is ExpressionSyntax initExpr &&
                condition is ExpressionSyntax condExpr &&
                update is ExpressionSyntax updateExpr &&
                body is StatementSyntax bodyStmt)
            {
                return ForStatement(
                    SingletonSeparatedList<ExpressionStatementSyntax>(ExpressionStatement(initExpr)),
                    SingletonSeparatedList(condExpr),
                    SingletonSeparatedList(updateExpr),
                    bodyStmt
                );
            }
            return null;
        }

        public override CSharpSyntaxNode VisitSwitchStatement([NotNull] GroovyParser.SwitchStatementContext context)
        {
            var switchExpr = Visit(context.expression());
            var sections = new List<SwitchSectionSyntax>();

            foreach (var caseStmt in context.caseStatement())
            {
                var caseExpr = Visit(caseStmt.expression());
                var caseBody = Visit(caseStmt.statement());

                if (caseExpr is ExpressionSyntax caseExprSyntax && 
                    caseBody is StatementSyntax caseBodyStmt)
                {
                    sections.Add(
                        SwitchSection()
                            .WithLabels(SingletonList<SwitchLabelSyntax>(
                                CaseSwitchLabel(caseExprSyntax)))
                            .WithStatements(SingletonList(caseBodyStmt))
                    );
                }
            }

            if (switchExpr is ExpressionSyntax switchExprSyntax)
            {
                return SwitchStatement(switchExprSyntax)
                    .WithSections(List(sections));
            }
            return null;
        }

        public override CSharpSyntaxNode VisitTryCatchFinallyStatement([NotNull] GroovyParser.TryCatchFinallyStatementContext context)
        {
            var tryBlock = Visit(context.tryBlock());
            var catches = new List<CatchClauseSyntax>();
            
            foreach (var catchBlock in context.catchBlock())
            {
                var exception = Visit(catchBlock.exception);
                var catchBody = Visit(catchBlock.body);

                if (exception is ParameterSyntax exceptionParam && 
                    catchBody is BlockSyntax catchBodyBlock)
                {
                    catches.Add(
                        CatchClause()
                            .WithDeclaration(CatchDeclaration(
                                ParseTypeName(exceptionParam.Type.ToString()),
                                Identifier(exceptionParam.Identifier.ToString())))
                            .WithBlock(catchBodyBlock)
                    );
                }
            }

            BlockSyntax finallyBlock = null;
            if (context.finallyBlock() != null)
            {
                var finallyNode = Visit(context.finallyBlock());
                if (finallyNode is BlockSyntax finallyBlockSyntax)
                {
                    finallyBlock = finallyBlockSyntax;
                }
            }

            if (tryBlock is BlockSyntax tryBlockSyntax)
            {
                return TryStatement(
                    tryBlockSyntax,
                    List(catches),
                    finallyBlock != null ? FinallyClause(finallyBlock) : null
                );
            }
            return null;
        }

        // Groovy-Specific Expressions

        public override CSharpSyntaxNode VisitGstringExpression([NotNull] GroovyParser.GstringExpressionContext context)
        {
            // Convert Groovy GString to C# string interpolation
            var expressions = new List<InterpolationSyntax>();
            
            foreach (var expr in context.gstringExpressionBody())
            {
                var interpolatedExpr = Visit(expr);
                if (interpolatedExpr is ExpressionSyntax interpolatedExprSyntax)
                {
                    expressions.Add(Interpolation(interpolatedExprSyntax));
                }
            }

            return InterpolatedStringExpression(
                Token(SyntaxKind.InterpolatedStringStartToken),
                List(expressions),
                Token(SyntaxKind.InterpolatedStringEndToken)
            );
        }

        public override CSharpSyntaxNode VisitClosureExpression([NotNull] GroovyParser.ClosureExpressionContext context)
        {
            // Convert Groovy closure to C# lambda expression
            var parameters = new List<ParameterSyntax>();
            
            if (context.argumentDeclarationList() != null)
            {
                foreach (var param in context.argumentDeclarationList().argumentDeclaration())
                {
                    parameters.Add(
                        Parameter(Identifier(param.parameterName.Text))
                            .WithType(ParseTypeName(param.parameterType.Text))
                    );
                }
            }

            var body = Visit(context.blockStatement());
            
            if (body is BlockSyntax bodyBlock)
            {
                return ParenthesizedLambdaExpression()
                    .WithParameterList(ParameterList(SeparatedList(parameters)))
                    .WithBlock(bodyBlock);
            }
            return null;
        }

        public override CSharpSyntaxNode VisitMapConstructor([NotNull] GroovyParser.MapConstructorContext context)
        {
            // Convert Groovy map constructor to C# dictionary initialization
            var entries = new List<ExpressionSyntax>();
            
            foreach (var entry in context.mapEntry())
            {
                var key = Visit(entry.key);
                var value = Visit(entry.value);
                
                if (key is ExpressionSyntax keyExpr && value is ExpressionSyntax valueExpr)
                {
                    entries.Add(
                        InitializerExpression(
                            SyntaxKind.ComplexElementInitializerExpression,
                            SeparatedList(new[] { keyExpr, valueExpr })
                        )
                    );
                }
            }

            return ObjectCreationExpression(
                GenericName(Identifier("Dictionary"))
                    .WithTypeArgumentList(
                        TypeArgumentList(
                            SeparatedList<TypeSyntax>(new[] {
                                ParseTypeName("string"),
                                ParseTypeName("object")
                            })
                        )
                    )
            )
            .WithInitializer(
                InitializerExpression(
                    SyntaxKind.CollectionInitializerExpression,
                    SeparatedList(entries)
                )
            );
        }

        public override CSharpSyntaxNode VisitListConstructor([NotNull] GroovyParser.ListConstructorContext context)
        {
            // Convert Groovy list constructor to C# list initialization
            var elements = new List<ExpressionSyntax>();
            
            foreach (var element in context.expression())
            {
                var expr = Visit(element);
                if (expr is ExpressionSyntax elementExpr)
                {
                    elements.Add(elementExpr);
                }
            }

            return ObjectCreationExpression(
                GenericName(Identifier("List"))
                    .WithTypeArgumentList(
                        TypeArgumentList(
                            SingletonSeparatedList<TypeSyntax>(
                                ParseTypeName("object")
                            )
                        )
                    )
            )
            .WithInitializer(
                InitializerExpression(
                    SyntaxKind.CollectionInitializerExpression,
                    SeparatedList(elements)
                )
            );
        }

        // Grails-Specific Patterns

        private CSharpSyntaxNode ConvertGormDynamicFinder(string methodName, ArgumentListSyntax arguments)
        {
            // Convert Grails GORM dynamic finders to C# LINQ expressions
            if (methodName.StartsWith("findBy"))
            {
                var propertyName = methodName.Substring(6); // Remove "findBy"
                return InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("DbSet"),
                        IdentifierName("FirstOrDefault")
                    )
                )
                .WithArgumentList(arguments);
            }
            
            return null;
        }

        private bool IsGrailsServiceInjection(FieldDeclarationSyntax field)
        {
            // Detect Grails service injection pattern
            return field.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name.ToString() == "Autowired");
        }

        // Grails Domain Class Processing
        
        private ClassDeclarationSyntax ProcessDomainClass(ClassDeclarationSyntax classDecl, GroovyParser.ClassDeclarationContext context)
        {
            var attributes = new List<AttributeListSyntax>();
            var members = new List<MemberDeclarationSyntax>();

            // Add Entity attribute
            attributes.Add(
                AttributeList(
                    SingletonSeparatedList(
                        Attribute(IdentifierName("Entity"))
                    )
                )
            );

            // Add Table attribute with class name
            attributes.Add(
                AttributeList(
                    SingletonSeparatedList(
                        Attribute(IdentifierName("Table"))
                        .WithArgumentList(
                            AttributeArgumentList(
                                SingletonSeparatedList(
                                    AttributeArgument(
                                        LiteralExpression(
                                            SyntaxKind.StringLiteralExpression,
                                            Literal(classDecl.Identifier.Text.ToLowerInvariant())
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            );

            // Process constraints block if present
            var constraintsBlock = FindConstraintsBlock(context);
            if (constraintsBlock != null)
            {
                ProcessConstraints(constraintsBlock, members);
            }

            // Add navigation properties for relationships
            ProcessRelationships(context, members);

            return classDecl
                .WithAttributeLists(List(attributes))
                .WithMembers(List(members));
        }

        private void ProcessConstraints(GroovyParser.BlockStatementContext constraintsBlock, List<MemberDeclarationSyntax> members)
        {
            foreach (var constraint in constraintsBlock.statement())
            {
                if (constraint is GroovyParser.ExpressionStatementContext exprStmt)
                {
                    var expr = exprStmt.expression();
                    if (expr is GroovyParser.BinaryExpressionContext binExpr && binExpr.op.Text == "=")
                    {
                        var propertyName = binExpr.left.GetText();
                        var constraints = binExpr.right;
                        
                        // Convert Grails constraints to C# data annotations
                        var attributes = new List<AttributeListSyntax>();
                        
                        if (constraints.GetText().Contains("blank: false"))
                        {
                            attributes.Add(
                                AttributeList(
                                    SingletonSeparatedList(
                                        Attribute(IdentifierName("Required"))
                                    )
                                )
                            );
                        }
                        
                        if (constraints.GetText().Contains("maxSize:"))
                        {
                            var maxSize = ExtractMaxSize(constraints.GetText());
                            attributes.Add(
                                AttributeList(
                                    SingletonSeparatedList(
                                        Attribute(IdentifierName("MaxLength"))
                                        .WithArgumentList(
                                            AttributeArgumentList(
                                                SingletonSeparatedList(
                                                    AttributeArgument(
                                                        LiteralExpression(
                                                            SyntaxKind.NumericLiteralExpression,
                                                            Literal(maxSize)
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            );
                        }

                        // Add the property with its constraints
                        members.Add(
                            PropertyDeclaration(
                                ParseTypeName("string"),
                                Identifier(propertyName)
                            )
                            .WithAttributeLists(List(attributes))
                            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                            .WithAccessorList(
                                AccessorList(
                                    List(new[]
                                    {
                                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                                        AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                                    })
                                )
                            )
                        );
                    }
                }
            }
        }

        private void ProcessRelationships(GroovyParser.ClassDeclarationContext context, List<MemberDeclarationSyntax> members)
        {
            foreach (var field in context.classBody().classMember())
            {
                if (field is GroovyParser.FieldDeclarationContext fieldDecl)
                {
                    var fieldType = fieldDecl.fieldType.Text;
                    
                    // Handle hasMany relationship
                    if (fieldType.Contains("hasMany"))
                    {
                        var relationshipType = ExtractRelationshipType(fieldType);
                        members.Add(
                            PropertyDeclaration(
                                GenericName(Identifier("ICollection"))
                                .WithTypeArgumentList(
                                    TypeArgumentList(
                                        SingletonSeparatedList<TypeSyntax>(
                                            ParseTypeName(relationshipType)
                                        )
                                    )
                                ),
                                Identifier(fieldDecl.fieldName.Text)
                            )
                            .WithAttributeLists(
                                SingletonList(
                                    AttributeList(
                                        SingletonSeparatedList(
                                            Attribute(IdentifierName("OneToMany"))
                                        )
                                    )
                                )
                            )
                            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                            .WithAccessorList(
                                AccessorList(
                                    List(new[]
                                    {
                                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                                        AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                                    })
                                )
                            )
                        );
                    }
                    
                    // Handle belongsTo relationship
                    else if (fieldType.Contains("belongsTo"))
                    {
                        var relationshipType = ExtractRelationshipType(fieldType);
                        members.Add(
                            PropertyDeclaration(
                                ParseTypeName(relationshipType),
                                Identifier(fieldDecl.fieldName.Text)
                            )
                            .WithAttributeLists(
                                SingletonList(
                                    AttributeList(
                                        SingletonSeparatedList(
                                            Attribute(IdentifierName("ManyToOne"))
                                        )
                                    )
                                )
                            )
                            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                            .WithAccessorList(
                                AccessorList(
                                    List(new[]
                                    {
                                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                                        AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                                            .WithSemicolonToken(Token(SyntaxKind.SemicolonToken))
                                    })
                                )
                            )
                        );
                    }
                }
            }
        }

        // Controller Processing
        
        private ClassDeclarationSyntax ProcessController(ClassDeclarationSyntax classDecl, GroovyParser.ClassDeclarationContext context)
        {
            var attributes = new List<AttributeListSyntax>();
            var members = new List<MemberDeclarationSyntax>();

            // Add Controller and Route attributes
            attributes.Add(
                AttributeList(
                    SingletonSeparatedList(
                        Attribute(IdentifierName("Controller"))
                    )
                )
            );

            attributes.Add(
                AttributeList(
                    SingletonSeparatedList(
                        Attribute(IdentifierName("Route"))
                        .WithArgumentList(
                            AttributeArgumentList(
                                SingletonSeparatedList(
                                    AttributeArgument(
                                        LiteralExpression(
                                            SyntaxKind.StringLiteralExpression,
                                            Literal($"api/[controller]")
                                        )
                                    )
                                )
                            )
                        )
                    )
                )
            );

            // Process controller actions
            foreach (var member in context.classBody().classMember())
            {
                if (member is GroovyParser.MethodDeclarationContext methodDecl)
                {
                    // Convert Grails actions to ASP.NET Core actions
                    var actionMethod = ProcessControllerAction(methodDecl);
                    if (actionMethod != null)
                    {
                        members.Add(actionMethod);
                    }
                }
            }

            return classDecl
                .WithAttributeLists(List(attributes))
                .WithMembers(List(members));
        }

        private MethodDeclarationSyntax ProcessControllerAction(GroovyParser.MethodDeclarationContext methodDecl)
        {
            var attributes = new List<AttributeListSyntax>();
            var httpMethod = DetermineHttpMethod(methodDecl);

            // Add HTTP method attribute
            attributes.Add(
                AttributeList(
                    SingletonSeparatedList(
                        Attribute(IdentifierName(httpMethod))
                    )
                )
            );

            // Add Route attribute if needed
            var routeTemplate = ExtractRouteTemplate(methodDecl);
            if (!string.IsNullOrEmpty(routeTemplate))
            {
                attributes.Add(
                    AttributeList(
                        SingletonSeparatedList(
                            Attribute(IdentifierName("Route"))
                            .WithArgumentList(
                                AttributeArgumentList(
                                    SingletonSeparatedList(
                                        AttributeArgument(
                                            LiteralExpression(
                                                SyntaxKind.StringLiteralExpression,
                                                Literal(routeTemplate)
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
            }

            // Convert method body
            var body = Visit(methodDecl.methodBody);

            return MethodDeclaration(
                ParseTypeName("IActionResult"),
                Identifier(methodDecl.methodName.Text)
            )
            .WithAttributeLists(List(attributes))
            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(ProcessActionParameters(methodDecl.argumentDeclarationList()))
            .WithBody(body as BlockSyntax);
        }

        // Helper methods
        private string DetermineHttpMethod(GroovyParser.MethodDeclarationContext methodDecl)
        {
            var methodName = methodDecl.methodName.Text.ToLower();
            return methodName switch
            {
                "save" => "HttpPost",
                "update" => "HttpPut",
                "delete" => "HttpDelete",
                "show" => "HttpGet",
                "index" => "HttpGet",
                _ => "HttpGet"
            };
        }

        private string ExtractRouteTemplate(GroovyParser.MethodDeclarationContext methodDecl)
        {
            var methodName = methodDecl.methodName.Text.ToLower();
            return methodName switch
            {
                "show" => "{id}",
                "update" => "{id}",
                "delete" => "{id}",
                _ => ""
            };
        }

        private ParameterListSyntax ProcessActionParameters(GroovyParser.ArgumentDeclarationListContext parameters)
        {
            var paramList = new List<ParameterSyntax>();

            if (parameters != null)
            {
                foreach (var param in parameters.argumentDeclaration())
                {
                    var attributes = new List<AttributeListSyntax>();
                    
                    // Add appropriate binding attributes
                    if (param.parameterType.Text.EndsWith("Command"))
{
    attributes.Add(
        AttributeList(
            SingletonSeparatedList(
                Attribute(IdentifierName("FromBody"))
            )
        )  // Add closing parenthesis here
    );
}
else if (param.parameterName.Text == "id")
{
    attributes.Add(
        AttributeList(
            SingletonSeparatedList(
                Attribute(IdentifierName("FromRoute"))
            )
        )  // Add closing parenthesis here
    );
}

paramList.Add(
    Parameter(Identifier(param.parameterName.Text))
        .WithAttributeLists(List(attributes))
                            .WithType(ParseTypeName(param.parameterType.Text))
                    );  
                }
            }

            return ParameterList(SeparatedList(paramList));
        }

        private string ExtractRelationshipType(string fieldType)
        {
            // Extract the actual type from hasMany or belongsTo declaration
            // This is a simplified version - you'll need to enhance it based on your specific needs
            var start = fieldType.IndexOf('<');
            var end = fieldType.IndexOf('>');
            if (start != -1 && end != -1)
            {
                return fieldType.Substring(start + 1, end - start - 1);
            }
            return fieldType;
        }

        private int ExtractMaxSize(string constraintText)
        {
            // Extract maxSize value from constraint text
            // This is a simplified version - you'll need to enhance it based on your specific needs
            var start = constraintText.IndexOf("maxSize:") + 8;
            var end = constraintText.IndexOf(',', start);
            if (end == -1) end = constraintText.Length;
            var sizeStr = constraintText.Substring(start, end - start).Trim();
            return int.Parse(sizeStr);
        }

        private GroovyParser.BlockStatementContext FindConstraintsBlock(GroovyParser.ClassDeclarationContext context)
        {
            // Find the constraints closure in the domain class
            foreach (var member in context.classBody().classMember())
            {
                if (member is GroovyParser.MethodDeclarationContext methodDecl &&
                    methodDecl.methodName.Text == "constraints")
                {
                    return methodDecl.methodBody as GroovyParser.BlockStatementContext;
                }
            }
            return null;
        }

        // Move your private methods inside the class
        private string GetIndentation()
        {
            return new string(' ', IndentLevel * 4);
        }

        private void IncreaseIndent()
        {
            IndentLevel++;
        }

        private void DecreaseIndent()
        {
            if (IndentLevel > 0)
                IndentLevel--;
        }
    }
}

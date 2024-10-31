namespace TYTCapstone;

public abstract class ASTNode
{
    public List<ASTNode> Children { get; set; } = new List<ASTNode>();
    public string Text { get; set; }
    public 
}

public class CompilationUnitNode : ASTNode
{
    // Additional properties specific to the compilation unit
}

public class ClassDeclarationNode : ASTNode
{
    public string ClassName { get; set; }
    // Additional properties
}

public class ASTListener : GroovyParserBaseListener
{
    private Stack<ASTNode> _stack = new Stack<ASTNode>();
    public ASTNode Root { get; private set; }

    public override void EnterCompilationUnit(GroovyParser.CompilationUnitContext context)
    {
        var node = new CompilationUnitNode
        {
            Text = context.GetText(),
            Line = context.Start.Line,
            Column = context.Start.Column
        };
        _stack.Push(node);
    }

    public override void ExitCompilationUnit(GroovyParser.CompilationUnitContext context)
    {
        var node = _stack.Pop();
        if (_stack.Count == 0)
        {
            Root = node; // This is the root of your AST
        }
        else
        {
            _stack.Peek().Children.Add(node);
        }
    }

    public override void EnterClassDeclaration(GroovyParser.ClassDeclarationContext context)
    {
        var node = new ClassDeclarationNode
        {
            ClassName = context.IDENTIFIER().GetText(),
            Text = context.GetText(),
            Line = context.Start.Line,
            Column = context.Start.Column
        };
        _stack.Push(node);
    }

    public override void ExitClassDeclaration(GroovyParser.ClassDeclarationContext context)
    {
        var node = _stack.Pop();
        _stack.Peek().Children.Add(node);
    }

    // Override other methods for different grammar rules
}
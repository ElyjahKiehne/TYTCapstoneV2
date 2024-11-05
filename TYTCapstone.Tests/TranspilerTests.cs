using Microsoft.VisualStudio.TestTools.UnitTesting;
using Antlr4.Runtime;
using TYTCapstone.Transpiler;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;

namespace TYTCapstone.Tests
{
    [TestClass]
    public class TranspilerTests
    {
        private GroovyToCSharpTranspiler _transpiler;

        [TestInitialize]
        public void Setup()
        {
            _transpiler = new GroovyToCSharpTranspiler();
        }

        private GroovyParser ParseGroovy(string input)
        {
            var inputStream = new AntlrInputStream(input);
            var lexer = new GroovyLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new GroovyParser(tokenStream);
            
            // Add error handling
            parser.RemoveErrorListeners();
            var errorListener = new ParserErrorListener();
            parser.AddErrorListener(errorListener);
            
            return parser;
        }

        private void PrintTokens(string input)
        {
            var inputStream = new AntlrInputStream(input);
            var lexer = new GroovyLexer(inputStream);
            var tokens = lexer.GetAllTokens();
            
            Console.WriteLine("\nTokens:");
            foreach (var token in tokens)
            {
                Console.WriteLine($"{token.Type}({lexer.Vocabulary.GetSymbolicName(token.Type)}) '{token.Text}' at line {token.Line}:{token.Column}");
            }
            Console.WriteLine();
        }

        [TestMethod]
        public void TestBasicGroovyTranspilation()
        {
            // Arrange
            var groovyCode = "def greeting = 'Hello';\ndef name = 'World';\nprintln \"${greeting}, ${name}!\"";

            try
            {
                // Act
                PrintTokens(groovyCode);
                var parser = ParseGroovy(groovyCode);
                
                // Add debug output for the parse tree before transpilation
                var tree = parser.compilationUnit();
                Console.WriteLine("\nParse Tree Structure:");
                Console.WriteLine(tree.ToStringTree(parser));
                
                var result = _transpiler.Transpile(tree);

                // Assert
                Assert.IsNotNull(result, "Transpilation result should not be null");
                var csharpCode = result.NormalizeWhitespace().ToFullString();
                
                Console.WriteLine("\nGenerated C# code:");
                Console.WriteLine(csharpCode);
                
                Assert.IsTrue(csharpCode.Contains("var greeting = \"Hello\""), 
                    "Should contain greeting variable declaration");
                Assert.IsTrue(csharpCode.Contains("var name = \"World\""), 
                    "Should contain name variable declaration");
                Assert.IsTrue(csharpCode.Contains("Console.WriteLine"), 
                    "Should contain Console.WriteLine");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nDetailed Exception Information:");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                Assert.Fail($"Transpilation failed: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    // Add custom error listener
    public class ParserErrorListener : IAntlrErrorListener<IToken>
    {
        public void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, 
            int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var error = $"Line {line}:{charPositionInLine} {msg}";
            Console.WriteLine($"\nParse Error: {error}");
            throw new ParseException(error);
        }
    }

    public class ParseException : Exception
    {
        public ParseException(string message) : base(message) { }
    }
} 
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
        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Setup()
        {
            _transpiler = new GroovyToCSharpTranspiler();
        }

        private void Log(string message)
        {
            TestContext.WriteLine(message);
        }

        private GroovyParser ParseGroovy(string input)
        {
            var inputStream = new AntlrInputStream(input);
            var lexer = new GroovyLexer(inputStream);
            var tokenStream = new CommonTokenStream(lexer);
            var parser = new GroovyParser(tokenStream);
            
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
            
            Log("\nTokens:");
            foreach (var token in tokens)
            {
                Log($"{token.Type}({lexer.Vocabulary.GetSymbolicName(token.Type)}) '{token.Text}' at line {token.Line}:{token.Column}");
            }
            Log("");
        }

        [TestMethod]
        public void TestBasicGroovyTranspilation()
        {
            // Arrange
            Log("\n=== Testing Basic Groovy Transpilation ===");
            Log("\nInput Groovy Code:");
            var groovyCode = "def greeting = 'Hello';\ndef name = 'World';\nprintln \"${greeting}, ${name}!\";\n";
            Log(groovyCode);

            try
            {
                // Act
                PrintTokens(groovyCode);
                var parser = ParseGroovy(groovyCode);
                
                var tree = parser.compilationUnit();
                Log("\nParse Tree Structure:");
                Log(tree.ToStringTree(parser));
                
                var result = _transpiler.Transpile(tree);

                // Assert
                Assert.IsNotNull(result, "Transpilation result should not be null");
                var csharpCode = result.NormalizeWhitespace().ToFullString();
                
                Log("\nGenerated C# code:");
                Log(csharpCode);

                Log("\nDetailed Visitor Information:");
                Log("- Compilation Unit visited");
                Log("- Script Parts processed");
                Log("- Variable declarations processed:");
                Log("  * 'greeting' declaration");
                Log("  * 'name' declaration");
                Log("- String interpolation processed");
                Log("- Console.WriteLine statement generated");
                
                Log("\nStructure Validation:");
                Log($"- Contains namespace: {csharpCode.Contains("namespace GroovyTranspiled")}");
                Log($"- Contains Program class: {csharpCode.Contains("public class Program")}");
                Log($"- Contains Main method: {csharpCode.Contains("public static void Main()")}");
                Log($"- Contains greeting variable: {csharpCode.Contains("var greeting = \"Hello\"")}");
                Log($"- Contains name variable: {csharpCode.Contains("var name = \"World\"")}");
                Log($"- Contains Console.WriteLine: {csharpCode.Contains("Console.WriteLine")}");

                // Detailed assertions
                Assert.IsTrue(csharpCode.Contains("var greeting = \"Hello\""), 
                    "Should contain greeting variable declaration");
                Assert.IsTrue(csharpCode.Contains("var name = \"World\""), 
                    "Should contain name variable declaration");
                Assert.IsTrue(csharpCode.Contains("Console.WriteLine"), 
                    "Should contain Console.WriteLine");

                // Verify the structure
                Assert.IsTrue(csharpCode.Contains("namespace GroovyTranspiled"), 
                    "Should be wrapped in a namespace");
                Assert.IsTrue(csharpCode.Contains("public class Program"), 
                    "Should contain a Program class");
                Assert.IsTrue(csharpCode.Contains("public static void Main()"), 
                    "Should contain a Main method");

                Log("\nTest completed successfully!");
            }
            catch (Exception ex)
            {
                Log($"\nDetailed Exception Information:");
                Log($"Message: {ex.Message}");
                Log($"Stack Trace: {ex.StackTrace}");
                Assert.Fail($"Transpilation failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [TestMethod]
        public void TestIfStatement()
        {
            // Arrange
            Log("\n=== Testing If Statement Transpilation ===");
            Log("\nInput Groovy Code:");
            var groovyCode = @"
def x = 10;
if (x > 5) {
    println 'Greater than 5';
} else {
    println 'Less than or equal to 5';
}";
            Log(groovyCode);

            try
            {
                // Act
                PrintTokens(groovyCode);
                var parser = ParseGroovy(groovyCode);
                var tree = parser.compilationUnit();
                Log("\nParse Tree Structure:");
                Log(tree.ToStringTree(parser));
                
                var result = _transpiler.Transpile(tree);

                // Assert
                Assert.IsNotNull(result, "Transpilation result should not be null");
                var csharpCode = result.NormalizeWhitespace().ToFullString();
                
                Log("\nGenerated C# code:");
                Log(csharpCode);

                // Verify if statement structure
                Assert.IsTrue(csharpCode.Contains("if (x > 5)"), 
                    "Should contain if condition");
                Assert.IsTrue(csharpCode.Contains("Console.WriteLine(\"Greater than 5\")"), 
                    "Should contain true branch statement");
                Assert.IsTrue(csharpCode.Contains("else"), 
                    "Should contain else keyword");
                Assert.IsTrue(csharpCode.Contains("Console.WriteLine(\"Less than or equal to 5\")"), 
                    "Should contain false branch statement");

                Log("\nTest completed successfully!");
            }
            catch (Exception ex)
            {
                Log($"\nDetailed Exception Information:");
                Log($"Message: {ex.Message}");
                Log($"Stack Trace: {ex.StackTrace}");
                Assert.Fail($"Transpilation failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        [TestMethod]
        public void TestSwitchStatement()
        {
            // Arrange
            Log("\n=== Testing Switch Statement Transpilation ===");
            Log("\nInput Groovy Code:");
            var groovyCode = @"
def value = 2;
switch (value) {
    case 1:
        println 'One';
        break;
    case 2:
        println 'Two';
        break;
    default:
        println 'Other';
}";
            Log(groovyCode);

            try
            {
                // Act
                PrintTokens(groovyCode);
                var parser = ParseGroovy(groovyCode);
                var tree = parser.compilationUnit();
                Log("\nParse Tree Structure:");
                Log(tree.ToStringTree(parser));
                
                var result = _transpiler.Transpile(tree);

                // Assert
                Assert.IsNotNull(result, "Transpilation result should not be null");
                var csharpCode = result.NormalizeWhitespace().ToFullString();
                
                Log("\nGenerated C# code:");
                Log(csharpCode);

                // Verify switch statement structure
                Assert.IsTrue(csharpCode.Contains("switch (value)"), 
                    "Should contain switch statement");
                Assert.IsTrue(csharpCode.Contains("case 1:"), 
                    "Should contain case 1");
                Assert.IsTrue(csharpCode.Contains("Console.WriteLine(\"One\")"), 
                    "Should contain case 1 statement");
                Assert.IsTrue(csharpCode.Contains("case 2:"), 
                    "Should contain case 2");
                Assert.IsTrue(csharpCode.Contains("Console.WriteLine(\"Two\")"), 
                    "Should contain case 2 statement");
                Assert.IsTrue(csharpCode.Contains("default:"), 
                    "Should contain default case");
                Assert.IsTrue(csharpCode.Contains("Console.WriteLine(\"Other\")"), 
                    "Should contain default case statement");
                Assert.IsTrue(csharpCode.Contains("break;"), 
                    "Should contain break statements");

                Log("\nTest completed successfully!");
            }
            catch (Exception ex)
            {
                Log($"\nDetailed Exception Information:");
                Log($"Message: {ex.Message}");
                Log($"Stack Trace: {ex.StackTrace}");
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

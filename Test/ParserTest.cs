using Redwood;
using Redwood.Ast;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Test
{
    public class ParserTest
    {
        [Fact]
        public async Task TestFunctionParsing()
        {
            string code = @"
function string testFunc()
{
    function string innerTestFunc(string paramA)
    {
        return paramA;
    }
    return innerTestFunc(""Test"");
}";


            Parser parser = new Parser(
                new StreamReader(
                    new MemoryStream(
                        Encoding.UTF8.GetBytes(code)
                    )
                )
            );
            FunctionDefinition function = await parser.ParseFunctionDefinition();
            Assert.NotNull(function);
            Lambda lambda = Compiler.CompileFunction(function);
            Assert.Equal(RedwoodType.GetForCSharpType(typeof(string)), lambda.ReturnType);
            Assert.Equal("Test", lambda.Run());
        }

        [Fact]
        public async Task TestFunctionClosure()
        {
            string code = @"
function string testFunc()
{
    let string varA = ""Test123"";
    function string innerTestFunc()
    {
        return varA;
    }
    return innerTestFunc();
}";

            Parser parser = new Parser(
                new StreamReader(
                    new MemoryStream(
                        Encoding.UTF8.GetBytes(code)
                    )
                )
            );
            FunctionDefinition function = await parser.ParseFunctionDefinition();
            Assert.NotNull(function);
            Lambda lambda = Compiler.CompileFunction(function);
            Assert.Equal(RedwoodType.GetForCSharpType(typeof(string)), lambda.ReturnType);
            Assert.Equal("Test123", lambda.Run());
        }
    }
}

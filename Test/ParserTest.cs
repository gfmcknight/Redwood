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
        private async Task<Lambda> MakeLambda(string code)
        {
            Parser parser = new Parser(
                new StreamReader(
                    new MemoryStream(
                        Encoding.UTF8.GetBytes(code)
                    )
                )
            );
            FunctionDefinition function = await parser.ParseFunctionDefinition();
            Assert.NotNull(function);
            return Compiler.CompileFunction(function);
        }

        [Fact]
        public async Task FunctionCanBeParsedAndCalled()
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


            Lambda lambda = await MakeLambda(code);
            Assert.Equal(RedwoodType.GetForCSharpType(typeof(string)), lambda.ReturnType);
            Assert.Equal("Test", lambda.Run());
        }

        [Fact]
        public async Task FunctionCanHoldClosureInformation()
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

            Lambda lambda = await MakeLambda(code);
            Assert.Equal(RedwoodType.GetForCSharpType(typeof(string)), lambda.ReturnType);
            Assert.Equal("Test123", lambda.Run());
        }

        [Fact]
        public async Task FunctionParameterCanBeClosured()
        {
            string code = @"
function string testFunc()
{
    function string outerTestFunc(string paramA)
    {
        function string innerTestFunc()
        {
            return paramA;
        }
        return innerTestFunc();
    }
    return outerTestFunc(""Test123"");
}";

            Lambda lambda = await MakeLambda(code);
            Assert.Equal("Test123", lambda.Run());
        }

        [Fact]
        public async Task CanUseIfStatements()
        {
            string code = @"
function string testFunc(bool addTest, bool add123)
{
    if addTest
    {
        if (add123)
        {
            return ""Test123"";
        }
        else
        {
            return ""Test"";
        }
    }

    if add123
    {
        return ""One, two, three!"";
    }
    else
    {
        return ""..."";
    }
}";

            Lambda lambda = await MakeLambda(code);
            Assert.Equal("One, two, three!", lambda.Run(false, true));
            Assert.Equal("...", lambda.Run(false, false));

            Assert.Equal("Test123", lambda.Run(true, true));
            Assert.Equal("Test", lambda.Run(true, false));

        }

        [Fact]
        public async Task CanUseAKeywordInVariableNames()
        {
            string code = @"
function string testFunc()
{
    function string returnExact(string paramA)
    {
        return paramA;
    }
    returnExact(""Test"");
    return returnExact("""");
}";

            Lambda lambda = await MakeLambda(code);
            Assert.Equal("", lambda.Run());
        }

        [Fact]
        public async Task CanCallOperators()
        {
            string code = @"
function string testFunc()
{
    return 2 + 6;
}";

            Lambda lambda = await MakeLambda(code);
            Assert.Equal(8, lambda.Run());
        }

        [Fact]
        public async Task CanConcatenateString()
        {
            string code = @"
function string testFunc()
{
    return ""Test"" + ""123"";
}";

            Lambda lambda = await MakeLambda(code);
            Assert.Equal("Test123", lambda.Run());
        }
    }
}

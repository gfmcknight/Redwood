using Redwood;
using Redwood.Ast;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

        private async Task<GlobalContext> MakeModule(string code)
        {
            Parser parser = new Parser(
                new StreamReader(
                    new MemoryStream(
                        Encoding.UTF8.GetBytes(code)
                    )
                )
            );
            TopLevel module = await parser.ParseModule();
            Assert.NotNull(module);
            return Compiler.CompileModule(module);
        }

        [Fact]
        public async Task FunctionCanBeParsedAndCalled()
        {
            string code = @"
function<string> testFunc()
{
    function<string> innerTestFunc(string paramA)
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
function<string> testFunc()
{
    let string varA = ""Test123"";
    function<string> innerTestFunc()
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
function<string> testFunc()
{
    function<string> outerTestFunc(string paramA)
    {
        function<string> innerTestFunc()
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
function<string> testFunc(bool addTest, bool add123)
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
function<string> testFunc()
{
    function<string> returnExact(string paramA)
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
function<int> testFunc()
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
function<string> testFunc()
{
    return ""Test"" + ""123"";
}";

            Lambda lambda = await MakeLambda(code);
            Assert.Equal("Test123", lambda.Run());
        }

        [Fact]
        public async Task OrderOfOperationsWorks()
        {
            string code = @"
function<int> testFunc()
{
    return 4 + 2 * 6 - 3 / 2;
}";

            Lambda lambda = await MakeLambda(code);
            Assert.Equal(15, lambda.Run());
        }

        [Fact]
        public async Task ParentheticalsWork()
        {
            string code = @"
function<int> testFunc()
{
    return (4 + 2) * (6 - 3);
}";

            Lambda lambda = await MakeLambda(code);
            Assert.Equal(18, lambda.Run());
        }

        [Fact]
        public async Task CanCallMethods()
        {
            string code = @"
function<int> testFunc()
{
    import Test.SampleClass;
    function<int> callMethod(SampleClass sc, int x)
    {
        return sc.SampleMethod(x);
    }
    return callMethod;
}";

            Compiler.ExposeAssembly(Assembly.GetExecutingAssembly());
            Lambda lambda = await MakeLambda(code);
            Lambda innerLambda = lambda.Run() as Lambda;
            SampleClass sc = new SampleClass
            {
                SampleField = 5
            };
            Assert.Equal(5, innerLambda.Run(sc, 11));
            Assert.Equal(11, sc.SampleField);
        }

        [Fact]
        public async Task CanParseModules()
        {
            string code = @"
import Test.SampleClass;
function<int> testFunc(SampleClass sc, int x)
{
    return sc.SampleMethod(x);
}";

            Compiler.ExposeAssembly(Assembly.GetExecutingAssembly());
            GlobalContext module = await MakeModule(code);
            Lambda lambda = module.LookupVariable("testFunc") as Lambda;
            SampleClass sc = new SampleClass
            {
                SampleField = 5
            };
            Assert.Equal(5, lambda.Run(sc, 11));
            Assert.Equal(11, sc.SampleField);
        }

        [Fact]
        public async Task CanResolveOperatorsOnKnownTypes()
        {
            string code = @"
import Test.SampleClass;
function<SampleClass> testFunc(SampleClass sc1, SampleClass sc2)
{
    return sc1 + sc2;
}";

            Compiler.ExposeAssembly(Assembly.GetExecutingAssembly());
            GlobalContext module = await MakeModule(code);
            Lambda lambda = module.LookupVariable("testFunc") as Lambda;
            SampleClass sc1 = new SampleClass
            {
                SampleField = 5,
                SampleProperty = 2
            };

            SampleClass sc2 = new SampleClass
            {
                SampleField = 7,
                SampleProperty = 2
            };
            object result = lambda.Run(sc1, sc2);
            Assert.IsType<SampleClass>(result);
            SampleClass scResult = result as SampleClass;
            Assert.Equal(12, scResult.SampleField);
            Assert.Equal(4, scResult.SampleProperty);
        }

        [Fact]
        public async Task CanAccessPropertiesAndFields()
        {
            string code = @"
import Test.SampleClass;
function<int> testFunc(SampleClass sc, int x)
{
    return sc.SampleField + sc.SampleProperty + x;
}";

            Compiler.ExposeAssembly(Assembly.GetExecutingAssembly());
            GlobalContext module = await MakeModule(code);
            Lambda lambda = module.LookupVariable("testFunc") as Lambda;
            SampleClass sc = new SampleClass
            {
                SampleField = 5,
                SampleProperty = 3
            };
            Assert.Equal(10, lambda.Run(sc, 2));
        }

        [Fact]
        public async Task CanAssignVariables()
        {
            string code = @"
function<int> testFunc()
{
    let int x = 2;
    x = x + 3;
    x = x + 5;
    x = x - 2;
    return x;
}";

            Lambda lambda = await MakeLambda(code);
            Assert.Equal(8, lambda.Run());
        }
    }
}

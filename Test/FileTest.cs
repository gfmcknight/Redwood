using Redwood;
using Redwood.Ast;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Xunit;

namespace Test
{
    public class FileTest
    {
        [Theory]
        [InlineData("simple_test.rwd")]
        [InlineData("class_def_test.rwd")]
        [InlineData("static_type_test.rwd")]
        [InlineData("overload_test.rwd")]
        public async void RunTestFromFile(string filename)
        {
            Compiler.ExposeAssembly(Assembly.GetExecutingAssembly());

            Parser parser = new Parser(new StreamReader("FileTests/" + filename));
            TopLevel top = await parser.ParseModule();
            GlobalContext context = Compiler.CompileModule(top);
            
            Lambda lambda = context.LookupVariable("testMain") as Lambda;
            Assert.NotNull(lambda);
            
            RAssert assert = new RAssert();
            lambda.Run(assert);
            Assert.NotEqual(0, assert.AssertionsCount);
        }
    }
}

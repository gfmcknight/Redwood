﻿using Redwood;
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
    public class FileTest
    {
        [Theory]
        [InlineData("simple_test.rwd")]
        [InlineData("class_def_test.rwd")]
        [InlineData("static_type_test.rwd")]
        [InlineData("overload_test.rwd")]
        [InlineData("module_test.rwd")]
        [InlineData("interop_test.rwd")]
        [InlineData("static_method_test.rwd")]
        [InlineData("linked_list_test.rwd")]
        [InlineData("operator_test.rwd")]
        [InlineData("interface_test.rwd")]
        [InlineData("implicit_conversions_test.rwd")]
        public async Task RunTestFromFile(string filename)
        {
            Compiler.ExposeAssembly(Assembly.GetExecutingAssembly());

            Parser parser = new Parser(new StreamReader("FileTests/" + filename));
            TopLevel top = await parser.ParseModule(
                Path.GetFileNameWithoutExtension(filename)
            );

            GlobalContext context = await Compiler.CompileModule(
                top,
                new DirectoryResourceProvider("FileTests/")
            );
            
            Lambda lambda = context.LookupVariable("testMain") as Lambda;
            Assert.NotNull(lambda);
            
            RAssert assert = new RAssert();
            lambda.Run(assert);
            Assert.NotEqual(0, assert.AssertionsCount);
        }
    }
}

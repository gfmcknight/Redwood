using Redwood;
using Redwood.Ast;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Test
{
    public class LambdaTest
    {
        [Fact]
        public void CanReturnSimpleValue()
        {
            Lambda lambda = Compiler.CompileFunction(
                new FunctionDefinition
                {
                    ClassMethod = false,
                    Name = "testFunc",
                    ReturnType = new TypeSyntax
                    {
                        TypeName = new NameExpression
                        {
                            Name = "string"
                        }
                    },
                    Parameters = new ParameterDefinition[] { },
                    Body = new BlockStatement
                    {
                        Statements = new Statement[]
                        {
                            new ReturnStatement
                            {
                                Expression = new StringConstant { Value = "Test" }
                            }
                        }
                    }
                }
            );

            Assert.Equal(RedwoodType.GetForCSharpType(typeof(string)), lambda.ReturnType);
            Assert.Equal("Test", lambda.Run());
        }

        [Fact]
        public void CanReturnValueFromParameter()
        {
            Lambda lambda = Compiler.CompileFunction(
                new FunctionDefinition
                {
                    ClassMethod = false,
                    Name = "testFunc",
                    ReturnType = new TypeSyntax
                    {
                        TypeName = new NameExpression
                        {
                            Name = "string"
                        }
                    },
                    Parameters = new ParameterDefinition[] 
                    {
                        new ParameterDefinition
                        {
                            Type = new TypeSyntax
                            {
                                TypeName = new NameExpression { Name = "string" },
                            },
                            Name = "paramA"
                        }
                    },
                    Body = new BlockStatement
                    {
                        Statements = new Statement[]
                        {
                            new ReturnStatement
                            {
                                Expression = new NameExpression { Name = "paramA" }
                            }
                        }
                    }
                }
            );

            Assert.Equal("TestABC", lambda.Run("TestABC"));
            Assert.Equal("Test123", lambda.Run("Test123"));
        }

        [Fact]
        public void CanCallAnotherFunction()
        {
            FunctionDefinition innerFunction = new FunctionDefinition
            {
                ClassMethod = false,
                Name = "innerTestFunc",
                ReturnType = new TypeSyntax
                {
                    TypeName = new NameExpression
                    {
                        Name = "string"
                    }
                },
                Parameters = new ParameterDefinition[]
                {
                    new ParameterDefinition
                    {
                        Type = new TypeSyntax
                        {
                            TypeName = new NameExpression { Name = "string" },
                        },
                        Name = "paramA"
                    }
                },
                Body = new BlockStatement
                {
                    Statements = new Statement[]
                    {
                        new ReturnStatement
                        {
                            Expression = new NameExpression { Name = "paramA" }
                        }
                    }
                }
            };

            Lambda lambda = Compiler.CompileFunction(
                new FunctionDefinition
                {
                    ClassMethod = false,
                    Name = "testFunc",
                    ReturnType = new TypeSyntax
                    {
                        TypeName = new NameExpression
                        {
                            Name = "string"
                        }
                    },
                    Parameters = new ParameterDefinition[] { },
                    Body = new BlockStatement
                    {
                        Statements = new Statement[] {
                            innerFunction,
                            new ReturnStatement
                            {
                                Expression = new CallExpression
                                {
                                    FunctionName = new NameExpression { Name = "innerTestFunc" },
                                    Arguments = new Expression[] { new StringConstant { Value = "Test" } }
                                }
                            }
                        }
                    }
                }
            );

            Assert.Equal("Test", lambda.Run());
        }
    }
}

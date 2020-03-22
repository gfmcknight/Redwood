using Redwood.Ast;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Redwood
{
    public class ParseError
    {
        public string Description { get; private set; }
        public int LineStart { get; private set; }
        public int LineEnd { get; private set; }
        public int ColumnStart { get; private set; }
        public int ColumnEnd { get; private set; }
    }
    public class Parser
    {
        private const int BufferSize = 1024;
        private static List<BinaryOpGroupParser> binaryOpParsers;

        enum TokenType
        {
            Token,
            Name,
            String,
            Int,
            Double
        }

        internal BigInteger LastInt { get; private set; }
        internal string LastString { get; private set; }
        internal string LastName { get; private set; }
        internal double LastDouble { get; private set; }

        private TokenType lastToken;
        private StreamReader reader;
        private char[] currentBuffer;
        private char[] lastBuffer;
        private int bufferPos;
        private int bufferEnd;
        private int lastBufferEnd;
        private bool wentBackBuffers;
        private bool wentBackTokens;
        private int lineCount;
        private bool initialized;
        private bool eof;

        public Parser(StreamReader reader)
        {
            this.reader = reader;
            currentBuffer = new char[BufferSize];
            lastBuffer = new char[BufferSize];
        }

        static Parser()
        {
            binaryOpParsers = new List<BinaryOpGroupParser>();

            // Assign - TODO this is the wrong association direction
            binaryOpParsers.Add(
                new BinaryOpGroupParser(new BinaryOpSpec("=", BinaryOperator.Assign, false))
            );

            // Null coalesce
            binaryOpParsers.Add(
                new BinaryOpGroupParser(new BinaryOpSpec("??", BinaryOperator.Coalesce, false))
            );

            // Logical Or
            binaryOpParsers.Add(
                new BinaryOpGroupParser(new BinaryOpSpec("||", BinaryOperator.LogicalOr, false))
            );
            // Logical And
            binaryOpParsers.Add(
                new BinaryOpGroupParser(new BinaryOpSpec("&&", BinaryOperator.LogicalAnd, false))
            );
            // Bitwise Or
            binaryOpParsers.Add(
                new BinaryOpGroupParser(new BinaryOpSpec("|", BinaryOperator.BitwiseOr, false))
            );
            // Bitwise Xor
            binaryOpParsers.Add(
                new BinaryOpGroupParser(new BinaryOpSpec("^", BinaryOperator.BitwiseXor, false))
            );
            // Bitwise And
            binaryOpParsers.Add(
                new BinaryOpGroupParser(new BinaryOpSpec("&", BinaryOperator.BitwiseAnd, false))
            );
            
            // Equal/NotEqual
            binaryOpParsers.Add(
                new BinaryOpGroupParser(
                    new BinaryOpSpec("==", BinaryOperator.Equals, false),
                    new BinaryOpSpec("!=", BinaryOperator.NotEquals, false)
                )
            );
            
            // Comparison
            binaryOpParsers.Add(
                new BinaryOpGroupParser(
                    new BinaryOpSpec("<=", BinaryOperator.LessThanOrEquals, false),
                    new BinaryOpSpec(">=", BinaryOperator.GreaterThanOrEquals, false),
                    new BinaryOpSpec("<", BinaryOperator.LessThan, false),
                    new BinaryOpSpec(">", BinaryOperator.GreaterThan, false)
                )
            );

            // Shift
            binaryOpParsers.Add(
                new BinaryOpGroupParser(
                    new BinaryOpSpec("<<", BinaryOperator.LeftShift, false),
                    new BinaryOpSpec(">>", BinaryOperator.RightShift, false)
                )
            );

            // Additive
            binaryOpParsers.Add(
                new BinaryOpGroupParser(
                    new BinaryOpSpec("+", BinaryOperator.Add, false),
                    new BinaryOpSpec("-", BinaryOperator.Subtract, false)
                )
            );

            // Multiplicative
            binaryOpParsers.Add(
                new BinaryOpGroupParser(
                    new BinaryOpSpec("*", BinaryOperator.Multiply, false),
                    new BinaryOpSpec("/", BinaryOperator.Divide, false),
                    new BinaryOpSpec("%", BinaryOperator.Modulus, false)
                )
            );

            for (int i = 0; i < binaryOpParsers.Count - 1; i++)
            {
                binaryOpParsers[i].next = binaryOpParsers[i + 1];
            }
        }

        private class BinaryOpSpec
        {
            public string token;
            public BinaryOperator op;
            public bool alphaNum;
            public BinaryOpSpec(string token, BinaryOperator op, bool alphaNum)
            {
                this.token = token;
                this.op = op;
                this.alphaNum = alphaNum;
            }
        }
        private class BinaryOpGroupParser
        {
            BinaryOpSpec[] operators;
            public BinaryOpGroupParser next;
            public BinaryOpGroupParser(params BinaryOpSpec[] operators)
            {
                this.operators = operators;
            }

            private async Task<Expression> ParseNext(Parser owner)
            {
                if (next == null)
                {
                    return await owner.ParsePrimaryTailExpression();
                }
                else
                {
                    return await next.Parse(owner);
                }
            }

            public async Task<BinaryOperator?> ParseLoneOperatorSymbol(Parser owner)
            {

                BinaryOperator? result = null;
                if (next != null)
                {
                    result = await next.ParseLoneOperatorSymbol(owner);
                }

                if (result != null)
                {
                    return result;
                }

                foreach (BinaryOpSpec op in operators)
                {
                    if (await owner.MaybeToken(op.token, op.alphaNum))
                    {
                        return op.op;
                    }
                }

                return null;
            }

            public async Task<Expression> Parse(Parser owner)
            {
                Expression leftMost = await ParseNext(owner);
                if (leftMost == null)
                {
                    throw new NotImplementedException();
                }

                while (!owner.eof)
                {
                    bool progress = false;
                    foreach (BinaryOpSpec op in operators)
                    {
                        if (await owner.MaybeToken(op.token, op.alphaNum))
                        {
                            Expression right = await ParseNext(owner);
                            if (right == null)
                            {
                                throw new NotImplementedException();
                            }
                            leftMost = new BinaryOperationExpression
                            {
                                Operator = op.op,
                                Left = leftMost,
                                Right = right
                            };
                            progress = true;
                            break;
                        }
                    }

                    if (!progress)
                    {
                        return leftMost;
                    }
                }

                throw new NotImplementedException();
            }
        }

        public async Task<TopLevel> ParseModule(string name = "main")
        {
            List<Definition> definitions = new List<Definition>();
            while (!eof)
            {
                Definition definition = await ParseClass();
                if (definition != null)
                {
                    definitions.Add(definition);
                    continue;
                }

                definition = await ParseDefinition();
                if (definition == null)
                {
                    break;
                }
                definitions.Add(definition);
            }

            return new TopLevel
            {
                ModuleName = name,
                Definitions = (definitions.ToArray())
            };
        }

        private async Task<BlockStatement> ParseBlock()
        {
            List<Statement> statements = new List<Statement>();
            if (await MaybeToken("{", false))
            {
                while (!eof)
                {
                    if (await MaybeToken("}", false))
                    {
                        break;
                    }

                    Statement statement = await ParseDefinition();
                    if (statement != null)
                    {
                        statements.Add(statement);
                        continue;
                    }

                    statement = await ParseStatementOrExpression();
                    if (statement == null)
                    {
                        throw new NotImplementedException();
                    }
                    statements.Add(statement);
                }
            }
            else
            {
                Statement statement = await ParseStatementOrExpression();
                if (statement == null)
                {
                    throw new NotImplementedException();
                }
                statements.Add(statement);
            }

            return new BlockStatement
            {
                Statements = statements.ToArray()
            };
        }

        internal async Task<ClassDefinition> ParseClass()
        {
            if (!await MaybeToken("class", true))
            {
                return null;
            }

            if (!await MaybeName())
            {
                throw new NotImplementedException();
            }
            string name = LastName;

            ParameterDefinition[] defaultParams = null;
            if (await MaybeToken("(", false))
            {
                if (await MaybeToken(")", false))
                {
                    defaultParams = new ParameterDefinition[0];
                }
                else
                {
                    defaultParams = await ParseParameterList();
                    if (!await MaybeToken(")", false))
                    {
                        throw new NotImplementedException();
                    }
                }
            }

            if (!await MaybeToken("{", false))
            {
                throw new NotImplementedException();
            }

            List<FunctionDefinition> constructors = new List<FunctionDefinition>();
            List<FunctionDefinition> methods = new List<FunctionDefinition>();
            List<FunctionDefinition> staticMethods = new List<FunctionDefinition>();
            List<LetDefinition> fields = new List<LetDefinition>();

            while (!eof)
            {
                if (await MaybeToken("}", false))
                {
                    break;
                }

                FunctionDefinition function = await ParseConstructorDefinition();
                if (function != null)
                {
                    constructors.Add(function);
                    continue;
                }

                function = await ParseFunctionDefinition();
                
                if (function == null)
                {
                    function = await ParseOperatorDefinition();
                }
                
                if (function != null)
                {
                    function.ClassMethod = true;
                    if (function.Static)
                    {
                        staticMethods.Add(function);
                    }
                    else
                    {
                        methods.Add(function);
                    }
                    continue;
                }

                LetDefinition field = await ParseLetDefinition();
                if (field != null)
                {
                    fields.Add(field);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            return new ClassDefinition
            {
                Name = name,
                Constructors = constructors.ToArray(),
                Methods = methods.ToArray(),
                StaticMethods = staticMethods.ToArray(),
                ParameterFields = defaultParams,
                InstanceFields =  fields.ToArray()
            };
        }

        internal async Task<Definition> ParseDefinition()
        {
            Definition definition = await ParseFunctionDefinition();
            if (definition != null)
            {
                return definition;
            }

            definition = await ParseLetDefinition();
            if (definition != null)
            {
                return definition;
            }

            definition = await ParseImportDefinition();
            if (definition != null)
            {
                return definition;
            }

            return null;
        }

        public async Task<FunctionDefinition> ParseFunctionDefinition()
        {
            bool isStatic = await MaybeToken("static", true);
            if (!await MaybeToken("function", true))
            {
                return null;
            }

            TypeSyntax returnType;
            if (await MaybeToken("<", false))
            {
                returnType = await ParseType();
                if (!await MaybeToken(">", false))
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                returnType = null;
            }

            if (!await MaybeName())
            {
                throw new NotImplementedException();
            }
            string functionName = LastName;

            if (!await MaybeToken("(", false))
            {
                throw new NotImplementedException();
            }
            ParameterDefinition[] parameters;
            if (await MaybeToken(")", false))
            {
                parameters = new ParameterDefinition[0];
            }
            else
            {
                parameters = await ParseParameterList();
                if (parameters == null)
                {
                    throw new NotImplementedException();
                }
                if (!await MaybeToken(")", false))
                {
                    throw new NotImplementedException();
                }
            }

            BlockStatement functionCode = await ParseBlock();
            if (functionCode == null)
            {
                throw new NotImplementedException();
            }

            return new FunctionDefinition
            {
                Static = isStatic,
                Name = functionName,
                Parameters = parameters,
                Body = functionCode,
                ReturnType = returnType
            };
        }

        public async Task<FunctionDefinition> ParseConstructorDefinition()
        {
            if (!await MaybeToken("constructor", true))
            {
                return null;
            }

            if (!await MaybeToken("(", false))
            {
                throw new NotImplementedException();
            }

            ParameterDefinition[] parameters;
            if (await MaybeToken(")", false))
            {
                parameters = new ParameterDefinition[0];
            }
            else
            {
                parameters = await ParseParameterList();
                if (parameters == null)
                {
                    throw new NotImplementedException();
                }
                if (!await MaybeToken(")", false))
                {
                    throw new NotImplementedException();
                }
            }

            BlockStatement functionCode = await ParseBlock();
            if (functionCode == null)
            {
                throw new NotImplementedException();
            }

            return new FunctionDefinition
            {
                Parameters = parameters,
                Body = functionCode
                // TODO: Return type?
            };
        }

        public async Task<FunctionDefinition> ParseOperatorDefinition()
        {
            if (!await MaybeToken("operator", true))
            {
                return null;
            }

            TypeSyntax returnType;
            if (await MaybeToken("<", false))
            {
                returnType = await ParseType();
                if (!await MaybeToken(">", false))
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                returnType = null;
            }

            string operatorName = null;

            BinaryOperator? binaryOperator = 
                await binaryOpParsers[0].ParseLoneOperatorSymbol(this);
            if (binaryOperator != null)
            {
                operatorName = RuntimeUtil.NameForOperator(binaryOperator.Value);
            }

            // TODO: Unary operators, implicit operator

            if (operatorName == null)
            {
                throw new NotImplementedException();
            }

            if (!await MaybeToken("(", false))
            {
                throw new NotImplementedException();
            }

            ParameterDefinition[] parameters;
            if (await MaybeToken(")", false))
            {
                parameters = new ParameterDefinition[0];
            }
            else
            {
                parameters = await ParseParameterList();
                if (parameters == null)
                {
                    throw new NotImplementedException();
                }
                if (!await MaybeToken(")", false))
                {
                    throw new NotImplementedException();
                }
            }

            BlockStatement functionCode = await ParseBlock();
            if (functionCode == null)
            {
                throw new NotImplementedException();
            }

            return new FunctionDefinition
            {
                Name = operatorName,
                Parameters = parameters,
                Body = functionCode,
                ReturnType = returnType,
                Static = true
            };
        }

        private async Task<LetDefinition> ParseLetDefinition()
        {
            if (!await MaybeToken("let", true))
            {
                return null;
            }

            TypeSyntax type = await ParseType();
            if (type == null)
            {
                throw new NotImplementedException();
            }

            if (!await MaybeName())
            {
                throw new NotImplementedException();
            }
            string variableName = LastName;

            Expression initializer = null;
            if (await MaybeToken("=", false))
            {
                initializer = await ParseExpression();
            }

            if (!await MaybeToken(";", false))
            {
                throw new NotImplementedException();
            }

            return new LetDefinition
            {
                Name = variableName,
                Type = type,
                Initializer = initializer
            };
        }
        
        private async Task<ImportDefinition> ParseImportDefinition()
        {
            if (!await MaybeToken("import", true))
            {
                return null;
            }
            // TODO: Only parse the non-dotwalk
            Expression expression = await ParsePrimaryTailExpression();
            if (!(expression is DotWalkExpression dwe))
            {
                throw new NotImplementedException();
            }
            if (!await MaybeToken(";", false))
            {
                throw new NotImplementedException();
            }

            return new ImportDefinition
            {
                NamespaceWalk = dwe
            };
        }

        private async Task<Statement> ParseStatementOrExpression()
        {
            Statement statement = await ParseReturn();
            if (statement != null)
            {
                return statement;
            }

            statement = await ParseIf();
            if (statement != null)
            {
                return statement;
            }

            statement = await ParseForStatement();
            if (statement != null)
            {
                return statement;
            }

            statement = await ParseExpression();
            if (statement != null)
            {
                if (!await MaybeToken(";", false))
                {
                    throw new NotImplementedException();
                }
                return statement;
            }

            return null;
        }

        private async Task<ReturnStatement> ParseReturn()
        {
            if (!await MaybeToken("return", true))
            {
                return null;
            }

            Expression expression = await ParseExpression();
            if (expression == null)
            {
                throw new NotImplementedException();
            }

            if (!await MaybeToken(";", false))
            {
                throw new NotImplementedException();
            }

            return new ReturnStatement
            {
                Expression = expression
            };
        }

        private async Task<IfStatement> ParseIf()
        {
            if (!await MaybeToken("if", true))
            {
                return null;
            }

            Expression condition = await ParseExpression();
            if (condition == null)
            {
                throw new NotImplementedException();
            }

            Statement pathTrue = await ParseBlock();
            if (pathTrue == null)
            {
                throw new NotImplementedException();
            }

            Statement elseStatement = null;
            if (await MaybeToken("else", true))
            {
                elseStatement = await ParseBlock();
                if (elseStatement == null)
                {
                    throw new NotImplementedException();
                }
            }

            return new IfStatement
            {
                Condition = condition,
                PathTrue = pathTrue,
                ElseStatement = elseStatement
            };
        }

        private async Task<ForStatement> ParseForStatement()
        {
            if (!await MaybeToken("for", true))
            {
                return null;
            }

            if (!await MaybeToken("(", false))
            {
                throw new NotImplementedException();
            }

            Statement initializer = await ParseLetDefinition();
            if (initializer == null)
            {
                initializer = await ParseExpression();
                if (!await MaybeToken(";", false))
                {
                    throw new NotImplementedException();
                }
            }

            Expression condition = await ParseExpression();
            if (!await MaybeToken(";", false))
            {
                throw new NotImplementedException();
            }

            Expression incrementor = await ParseExpression();
            if (!await MaybeToken(")", false))
            {
                throw new NotImplementedException();
            }

            BlockStatement body = await ParseBlock();
            if (body == null)
            {
                // TODO: Does this allow single semicolon for
                // loops? Should they be allowed?
                throw new NotImplementedException();
            }

            return new ForStatement
            {
                Initializer = initializer,
                Condition = condition,
                Incrementor = incrementor,
                Body = body
            };
        }

        private async Task<Expression> ParseExpression()
        {
            // TODO: lambda expression?
            return await binaryOpParsers[0].Parse(this);
        }

        private async Task<TypeSyntax> ParseType()
        {
            if (await MaybeToken("?", false))
            {
                return new TypeSyntax
                {
                    TypeName = new NameExpression
                    {
                        Name = "?"
                    }
                };

            }

            if (!await MaybeName())
            {
                return null;
            }

            string typename = LastName;
            if (!await MaybeToken("<", false))
            {
                return new TypeSyntax
                {
                    TypeName = new NameExpression
                    {
                        Name = typename
                    }
                };
            }

            List<TypeSyntax> innerTypes = new List<TypeSyntax>();
            while (!eof)
            {
                TypeSyntax innerType = await ParseType();
                if (innerType == null)
                {
                    throw new NotImplementedException();
                }

                if (await MaybeToken(">", false))
                {
                    break;
                }
                else if (!await MaybeToken(",", false))
                {
                    throw new NotImplementedException();
                }
            }

            return new TypeSyntax
            {
                TypeName = new NameExpression
                {
                    Name = typename,
                },
                GenericInnerTypes = innerTypes.ToArray()
            };
        }

        private async Task<ParameterDefinition[]> ParseParameterList()
        {
            List<ParameterDefinition> parameters = new List<ParameterDefinition>();
            while (!eof)
            {
                TypeSyntax type = await ParseType();
                if (type == null)
                {
                    throw new NotImplementedException();
                }

                if (!await MaybeName())
                {
                    throw new NotImplementedException();
                }

                parameters.Add(new ParameterDefinition
                {
                    Name = LastName,
                    Type = type
                });

                if (!await MaybeToken(",", false))
                {
                    return parameters.ToArray();
                }
            }
            return null;
        }

        private async Task<Expression[]> ParseArgumentList()
        {
            List<Expression> arguments = new List<Expression>();
            while (!eof)
            {
                Expression arg = await ParseExpression();
                if (arg == null)
                {
                    return arguments.ToArray();
                }
                arguments.Add(arg);
                if (!await MaybeToken(",", false))
                {
                    return arguments.ToArray();
                }
            }
            return null;
        }

        private async Task<Expression> ParsePrimaryTailExpression()
        {
            Expression leftMost = null;

            if (await MaybeToken("statictype", true))
            {
                if (!await MaybeToken("(", false))
                {
                    throw new NotImplementedException();
                }
                leftMost = await ParseExpression();
                if (leftMost == null)
                {
                    throw new NotImplementedException();
                }
                if (!await MaybeToken(")", false))
                {
                    throw new NotImplementedException();
                }

                leftMost = new StaticTypeExpression
                {
                    Expression = leftMost
                };
            }
            else if (await MaybeToken("type", true))
            {
                if (!await MaybeToken("(", false))
                {
                    throw new NotImplementedException();
                }
                leftMost = await ParseType();
                if (leftMost == null)
                {
                    throw new NotImplementedException();
                }
                if (!await MaybeToken(")", false))
                {
                    throw new NotImplementedException();
                }
            }
            else if (await MaybeToken("tag", true))
            {
                if (!await MaybeToken("(", false))
                {
                    throw new NotImplementedException();
                }

                if (!await MaybeString())
                {
                    throw new NotImplementedException();
                }

                string tag = LastString;

                if (!await MaybeToken(")", false))
                {
                    throw new NotImplementedException();
                }

                return new TagExpression
                {
                    Tag = new StringConstant
                    {
                        Value = tag
                    }
                };
            }
            else if (await MaybeToken("(", false))
            {
                // Parse a parenthetical expression
                leftMost = await ParseExpression();
                if (leftMost == null)
                {
                    throw new NotImplementedException();
                }
                if (!await MaybeToken(")", false))
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                leftMost = ParseUnary();
            }

            if (leftMost != null)
            {

            }
            else if (await MaybeToken("null", true))
            {
                // In theory, null cannot be dotwalked, called, etc.
                // but that might REALLY mess up our parsing
                leftMost = new NullExpression { };
            }
            else if (await MaybeToken("true", true))
            {
                leftMost = new BoolConstant
                {
                    Value = true
                };
            }
            else if (await MaybeToken("false", true))
            {
                leftMost = new BoolConstant
                {
                    Value = false
                };
            }
            else if (await MaybeName())
            {
                leftMost = new NameExpression
                {
                    Name = LastName
                };
            }
            else if (await MaybeInt())
            {
                leftMost = new IntConstant
                {
                    Value = LastInt
                };
            }
            else if (await MaybeDouble())
            {
                throw new NotImplementedException();
            }
            else if (await MaybeString())
            {
                leftMost = new StringConstant
                {
                    Value = LastString
                };
            }

            while (!eof)
            {
                if (leftMost is NameExpression ne && await MaybeToken("(", false))
                {
                    Expression[] args;
                    if (await MaybeToken(")", false))
                    {
                        args = new Expression[0];
                    }
                    else
                    {
                        args = await ParseArgumentList();
                        if (!await MaybeToken(")", false))
                        {
                            throw new NotImplementedException();
                        }
                    }

                    leftMost = new CallExpression
                    {
                        FunctionName = ne,
                        Arguments = args
                    };
                }
                else if (await MaybeToken("[", false))
                {
                    throw new NotImplementedException();
                }
                else if (await MaybeToken(".", false))
                {
                    if (!await MaybeName())
                    {
                        throw new NotImplementedException();
                    }

                    ne = new NameExpression
                    {
                        Name = LastName
                    };

                    if (await MaybeToken("(", false))
                    {
                        Expression[] args;
                        if (await MaybeToken(")", false))
                        {
                            args = new Expression[0];
                        }
                        else
                        {
                            args = await ParseArgumentList();
                            if (!await MaybeToken(")", false))
                            {
                                throw new NotImplementedException();
                            }
                        }

                        leftMost = new CallExpression
                        {
                            Callee = leftMost,
                            FunctionName = ne,
                            Arguments = args
                        };
                    }
                    else
                    {
                        leftMost = new DotWalkExpression
                        {
                            Chain = leftMost,
                            Element = ne
                        };
                    }
                }
                else if (await MaybeToken("++", false))
                {
                    leftMost = new UnaryOperationExpression
                    {
                        InnerExpression = leftMost,
                        Operator = UnaryOperator.PostIncrement
                    };
                }
                else if (await MaybeToken("--", false))
                {
                    leftMost = new UnaryOperationExpression
                    {
                        InnerExpression = leftMost,
                        Operator = UnaryOperator.PostDecrement
                    };
                }
                else
                {
                    return leftMost;
                }
            }

            throw new NotImplementedException();
        }

        private Expression ParseUnary()
        {
            // TODO
            return null;
        }

        private async Task SwapBuffers()
        {
            char[] temp = lastBuffer;
            lastBuffer = currentBuffer;
            currentBuffer = temp;

            bufferPos = 0;
            if (wentBackBuffers)
            {
                bufferEnd = lastBufferEnd;
                wentBackBuffers = false;
            }
            else
            {
                bufferEnd = await reader.ReadBlockAsync(currentBuffer);
            }
        }

        private async Task Advance()
        {
            if (eof)
            {
                return;
            }

            if (!initialized)
            {
                await SwapBuffers();
                initialized = true;
                return;
            }

            bufferPos++;
            if (bufferPos >= BufferSize)
            {
                await SwapBuffers();
            }
            if (bufferPos >= bufferEnd)
            {
                eof = true;
            }
        }

        private async Task AdvanceToNextToken()
        {
            if (!initialized)
            {
                await Advance();
            }

            while (!eof && char.IsWhiteSpace(currentBuffer[bufferPos]))
            {
                if (currentBuffer[bufferPos] == '\n')
                {
                    lineCount++;
                }
                await Advance();
            }
        }

        private void Unget(int amount)
        {
            bufferPos -= amount;
            // We rolled back from EOF, which means there's still a chance
            // for us to parse the file to its entirety a different way
            if (amount > 0)
            {
                eof = false;
            }

            if (bufferPos < 0)
            {
                bufferPos += BufferSize;
                lastBufferEnd = bufferEnd;

                char[] temp = lastBuffer;
                lastBuffer = currentBuffer;
                currentBuffer = temp;
                bufferEnd = BufferSize;
                wentBackBuffers = true;
            }
        }

        private async Task<bool> MaybeName()
        {
            if (wentBackTokens && lastToken == TokenType.Name)
            {
                wentBackTokens = false;
                return true;
            }

            await AdvanceToNextToken();
            // The first letter of the identifier must be a letter, but numbers
            // can follow
            if (!char.IsLetter(currentBuffer[bufferPos]) && currentBuffer[bufferPos] != '_')
            {
                return false;
            }

            StringBuilder sb = new StringBuilder();
            do
            {
                sb.Append(currentBuffer[bufferPos]);
                await Advance();
            } while ((char.IsLetterOrDigit(currentBuffer[bufferPos]) ||
                      currentBuffer[bufferPos] == '_') && !eof);

            LastName = sb.ToString();
            lastToken = TokenType.Name;
            return true;
        }

        private async Task<bool> MaybeToken(string token, bool alphaNumeric)
        {
            await AdvanceToNextToken();
            for (int i = 0; i < token.Length; i++)
            {
                if (eof || currentBuffer[bufferPos] != token[i])
                {
                    Unget(i);
                    return false;
                }
                await Advance();
            }

            // If someone has a name that STARTS with a keyword, don't grab the
            // first half of the name instead of the whole name
            if (alphaNumeric && char.IsLetterOrDigit(currentBuffer[bufferPos]))
            {
                Unget(token.Length);
                return false;
            }

            lastToken = TokenType.Token;
            return true;
        }

        private async Task<bool> MaybeString()
        {
            if (wentBackTokens && lastToken == TokenType.String)
            {
                wentBackTokens = false;
                return true;
            }

            if (!await MaybeToken("\"", false))
            {
                return false;
            }
            else
            {
                StringBuilder sb = new StringBuilder();
                while (!eof)
                {
                    // This quote is guaranteed to be unescaped
                    if (currentBuffer[bufferPos] == '"')
                    {
                        // Make sure we fully consume this thing
                        await Advance();
                        LastString = sb.ToString();
                        lastToken = TokenType.String;
                        return true;
                    }

                    if (currentBuffer[bufferPos] == '\\')
                    {
                        await Advance();
                        switch (currentBuffer[bufferPos])
                        {
                            case 'n':
                                sb.Append('\n');
                                break;
                            case '\\':
                                sb.Append('\\');
                                break;
                            case '\t':
                                sb.Append('\t');
                                break;
                            default:
                                throw new NotImplementedException();
                        }
                    }
                    else
                    {
                        sb.Append(currentBuffer[bufferPos]);
                    }
                    await Advance();
                }
            }

            // TODO: Throw!
            throw new NotImplementedException();
        }

        private bool IsFloatingMarker(char c)
        {
            return c == 'f' || c == 'd' || c == '.';
        }

        private async Task<bool> MaybeInt()
        {
            if (wentBackTokens && lastToken == TokenType.Int)
            {
                wentBackTokens = false;
                return true;
            }

            bool ValidIntChar(char c, bool hex)
            {
                return currentBuffer[bufferPos] >= '0' && currentBuffer[bufferPos] <= '9' ||
                       hex && currentBuffer[bufferPos] >= 'A' && currentBuffer[bufferPos] <= 'F' ||
                       hex && currentBuffer[bufferPos] >= 'a' && currentBuffer[bufferPos] <= 'f';
            }

            await AdvanceToNextToken();
            NumberStyles style;
            bool hex;
            if (await MaybeToken("0x", false))
            {
                hex = true;
                style = NumberStyles.HexNumber;
            }
            else
            {
                hex = false;
                style = NumberStyles.Integer;
            }

            if (!ValidIntChar(currentBuffer[bufferPos], hex))
            {
                Unget(hex ? 2 : 0);
                return false;
            }

            StringBuilder sb = new StringBuilder();
            while (!eof)
            {
                if (ValidIntChar(currentBuffer[bufferPos], hex))
                {
                    sb.Append(currentBuffer[bufferPos]);
                    await Advance();
                }
                else
                {
                    if (IsFloatingMarker(currentBuffer[bufferPos]))
                    {
                        Unget(sb.Length + (hex ? 2 : 0));
                        return false;
                    }
                    break;
                }
            }

            LastInt = BigInteger.Parse(sb.ToString(), style);
            lastToken = TokenType.Int;
            return true;
        }

        private async Task<bool> MaybeDouble()
        {
            if (wentBackTokens && lastToken == TokenType.Double)
            {
                wentBackTokens = false;
                return true;
            }

            await AdvanceToNextToken();
            StringBuilder sb = new StringBuilder();
            if (!char.IsDigit(currentBuffer[bufferPos]))
            {
                return false;
            }

            while (!eof)
            {
                if (!char.IsDigit(currentBuffer[bufferPos]) && !IsFloatingMarker(currentBuffer[bufferPos]))
                {
                    break;
                }
                sb.Append(currentBuffer[bufferPos]);
                await Advance();
            }

            LastDouble = double.Parse(sb.ToString());
            lastToken = TokenType.Double;
            return true;
        }

        internal void UngetLastToken()
        {
            wentBackTokens = true;
        }
    }
}

﻿using Redwood.Ast;
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

        public async Task<Statement> TryParse()
        {


            return null;
        }

        private async Task<BlockStatement> ParseBlock()
        {
            List<Statement> statements = new List<Statement>();
            if (await MaybeToken("{"))
            {
                while (!eof)
                {
                    if (await MaybeToken("}"))
                    {
                        break;
                    }

                    Statement statement = await ParseDefinition();
                    if (statement != null)
                    {
                        statements.Add(statement);
                        continue;
                    }

                    statement = await ParseReturnStatement();
                    if (statement != null)
                    {
                        statements.Add(statement);
                        continue;
                    }

                    // TODO: control structures

                    statement = await ParseExpression();
                    if (statement != null)
                    {
                        if (!await MaybeToken(";"))
                        {
                            throw new NotImplementedException();
                        }
                        statements.Add(statement);
                        continue;
                    }

                    throw new NotImplementedException();
                }
            }
            else
            {
                statements.Add(await ParseExpression());
            }

            return new BlockStatement
            {
                Statements = statements.ToArray()
            };
        }

        public async Task<Definition> ParseDefinition()
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

            return null;
        }

        public async Task<FunctionDefinition> ParseFunctionDefinition()
        {
            bool isStatic = await MaybeToken("static");
            if (!await MaybeToken("function"))
            {
                return null;
            }

            TypeSyntax returnType = await ParseType();
            if (returnType == null)
            {
                throw new NotImplementedException();
            }

            if (!await MaybeName())
            {
                throw new NotImplementedException();
            }
            string functionName = LastName;

            if (!await MaybeToken("("))
            {
                throw new NotImplementedException();
            }
            ParameterDefinition[] parameters;
            if (await MaybeToken(")"))
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
                if (!await MaybeToken(")"))
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

        private async Task<LetDefinition> ParseLetDefinition()
        {
            if (!await MaybeToken("let"))
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
            if (await MaybeToken("="))
            {
                initializer = await ParseExpression();
            }

            if (!await MaybeToken(";"))
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

        private async Task<ReturnStatement> ParseReturnStatement()
        {
            if (!await MaybeToken("return"))
            {
                return null;
            }

            Expression expression = await ParseExpression();
            if (expression == null)
            {
                throw new NotImplementedException();
            }

            if (!await MaybeToken(";"))
            {
                throw new NotImplementedException();
            }

            return new ReturnStatement
            {
                Expression = expression
            };
        }

        private async Task<Expression> ParseExpression()
        {
            // TODO
            return await ParseBaseItemAndTail();
        }

        private async Task<TypeSyntax> ParseType()
        {
            if (!await MaybeName())
            {
                return null;
            }

            string typename = LastName;
            if (!await MaybeToken("<"))
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

                if (await MaybeToken(">"))
                {
                    break;
                }
                else if (!await MaybeToken(","))
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

                if (!await MaybeToken(","))
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
                if (!await MaybeToken(","))
                {
                    return arguments.ToArray();
                }
            }
            return null;
        }

        private async Task<Expression> ParseBaseItemAndTail()
        {
            Expression leftMost = null;
            if (await MaybeName())
            {
                leftMost = new NameExpression
                {
                    Name = LastName
                };
            }
            else if (await MaybeInt())
            {
                throw new NotImplementedException();
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
                if (await MaybeToken("(") && leftMost is NameExpression ne)
                {
                    Expression[] args;
                    if (await MaybeToken(")"))
                    {
                        args = new Expression[0];
                    }
                    else
                    {
                        args = await ParseArgumentList();
                        if (!await MaybeToken(")"))
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
                else if (await MaybeToken("["))
                {
                    throw new NotImplementedException();
                }
                else if (await MaybeToken("."))
                {
                    if (!await MaybeName())
                    {
                        throw new NotImplementedException();
                    }

                    ne = new NameExpression
                    {
                        Name = LastName
                    };

                    if (await MaybeToken("("))
                    {
                        Expression[] args;
                        if (await MaybeToken(")"))
                        {
                            args = new Expression[0];
                        }
                        else
                        {
                            args = await ParseArgumentList();
                            if (!await MaybeToken(")"))
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
                else
                {
                    return leftMost;
                }
            }

            throw new NotImplementedException();
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
            if (bufferPos > BufferSize)
            {
                await SwapBuffers();
            }
            if (bufferPos > bufferEnd)
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

            while (char.IsWhiteSpace(currentBuffer[bufferPos]) && !eof)
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
            if (!char.IsLetter(currentBuffer[bufferPos]))
            {
                return false;
            }

            StringBuilder sb = new StringBuilder();
            do
            {
                sb.Append(currentBuffer[bufferPos]);
                await Advance();
            } while (char.IsLetterOrDigit(currentBuffer[bufferPos]) && !eof);

            LastName = sb.ToString();
            lastToken = TokenType.Name;
            return true;
        }

        private async Task<bool> MaybeToken(string token)
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

            if (await MaybeToken("\""))
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
                                // TODO: throw!
                                break;
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
            return false;
        }

        private bool isFloatingMarker(char c)
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

            bool validIntChar(char c, bool hex)
            {
                return currentBuffer[bufferPos] >= '0' && currentBuffer[bufferPos] <= '9' ||
                       hex && currentBuffer[bufferPos] >= 'A' && currentBuffer[bufferPos] <= 'F' ||
                       hex && currentBuffer[bufferPos] >= 'a' && currentBuffer[bufferPos] <= 'f';
            }

            await AdvanceToNextToken();
            NumberStyles style;
            bool hex;
            if (await MaybeToken("0x"))
            {
                hex = true;
                style = NumberStyles.HexNumber;
            }
            else
            {
                hex = false;
                style = NumberStyles.Integer;
            }

            if (!validIntChar(currentBuffer[bufferPos], hex))
            {
                Unget(hex ? 2 : 0);
                return false;
            }

            StringBuilder sb = new StringBuilder();
            while (!eof)
            {
                if (validIntChar(currentBuffer[bufferPos], hex))
                {
                    sb.Append(currentBuffer[bufferPos]);
                    await Advance();
                }
                else
                {
                    if (isFloatingMarker(currentBuffer[bufferPos]))
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
                if (!char.IsDigit(currentBuffer[bufferPos]) && !isFloatingMarker(currentBuffer[bufferPos]))
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

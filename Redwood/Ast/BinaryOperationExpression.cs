﻿using Redwood.Instructions;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Redwood.Ast
{
    public enum BinaryOperator
    {
        Multiply,
        Divide,
        Modulus,
        Add,
        Subtract,
        LeftShift,
        RightShift,
        LessThan,
        GreaterThan,
        LessThanOrEquals,
        GreaterThanOrEquals,
        Equals,
        NotEquals,
        BitwiseAnd,
        BitwiseXor,
        BitwiseOr,
        LogicalAnd,
        LogicalOr,
        Coalesce,
        Assign // TODO: Other assign operators?
    }
    public class BinaryOperationExpression : Expression
    {
        public BinaryOperator Operator { get; set; }
        public Expression Left { get; set; }
        public Expression Right { get; set; }

        internal List<Variable> TemporaryVariables { get; set; }
        internal string ReflectedOperatorName { get; set; }

        internal Lambda ResolvedOperator { get; set; }

        internal override IEnumerable<Instruction> Compile()
        {
            if (SpecialHandling(Operator))
            {
                switch (Operator)
                {
                    case BinaryOperator.Assign:
                        return CompileAssign();
                    default:
                        break;
                }
            }

            List<Instruction> instructions = new List<Instruction>();
            instructions.AddRange(Left.Compile());
            instructions.Add(Compiler.CompileVariableAssign(TemporaryVariables[0]));
            instructions.AddRange(Right.Compile());
            instructions.Add(Compiler.CompileVariableAssign(TemporaryVariables[1]));

            if (ResolvedOperator != null)
            {

                int[] argumentLocations = new int[]
                {
                    TemporaryVariables[0].Location,
                    TemporaryVariables[1].Location
                };
                // Load and run the lambda on the objects
                instructions.Add(new LoadConstantInstruction(ResolvedOperator));
                if (ResolvedOperator is InPlaceLambda)
                {
                    instructions.Add(new InPlaceCallInstruction(argumentLocations));
                }
                else if (ResolvedOperator is InternalLambda)
                {
                    instructions.Add(new InternalCallInstruction(argumentLocations));
                }
                else
                {
                    instructions.Add(new ExternalCallInstruction(argumentLocations));
                }
            }
            else
            {
                instructions.Add(new LookupExternalMemberBinaryOperationInstruction(
                    Operator,
                    TemporaryVariables[0].Location,
                    TemporaryVariables[1].Location,
                    Left.GetKnownType(),
                    Right.GetKnownType()));

                instructions.Add(new TryCallInstruction(
                    new RedwoodType[]
                    {
                        Left.GetKnownType(),
                        Right.GetKnownType()
                    },
                    new int[]
                    {
                        TemporaryVariables[0].Location,
                        TemporaryVariables[1].Location
                    })
                );
            }


            return instructions;
        }

        private IEnumerable<Instruction> CompileAssign()
        {
            List<Instruction> instructions = new List<Instruction>();
            if (Left is NameExpression ne)
            {
                instructions.AddRange(Right.Compile());
                instructions.Add(Compiler.CompileVariableAssign(ne.Variable));
            }
            else
            {
                // TODO: setting values on members
                throw new NotImplementedException();
            }
            return instructions.ToArray();
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            ReflectedOperatorName = RuntimeUtil.NameForOperator(Operator);
            TemporaryVariables = new List<Variable>();
            if (ReflectedOperatorName == null)
            {
                switch (Operator)
                {
                    case BinaryOperator.Assign:
                        if (Left is NameExpression ne)
                        {
                            ne.InLVal = true;
                        }
                        // TODO?
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
            else
            {
                TemporaryVariables.Add(new Variable
                {
                    Temporary = true
                });
                TemporaryVariables.Add(new Variable
                {
                    Temporary = true
                });
            }

            List<NameExpression> freeVars = Left.Walk().ToList();
            freeVars.AddRange(Right.Walk());
            return freeVars;
        }

        internal override void Bind(Binder binder)
        {
            binder.Bookmark();

            foreach (Variable variable in TemporaryVariables)
            {
                binder.BindVariable(variable);
            }
            
            Left.Bind(binder);
            Right.Bind(binder);

            binder.Checkout();

            if (!SpecialHandling(Operator) &&
                Left.GetKnownType() != null && Right.GetKnownType() != null)
            {
                // Resolve the static method
                MemberResolver.TryResolveOperator(
                    null,
                    null,
                    Left.GetKnownType(),
                    Right.GetKnownType(),
                    Operator,
                    out Lambda binaryOperationLambda);

                if (binaryOperationLambda == null)
                {
                    throw new NotImplementedException();
                }

                ResolvedOperator = binaryOperationLambda;
            }
        }

        public override RedwoodType GetKnownType()
        {
            return ResolvedOperator?.ReturnType;
        }

        internal override IEnumerable<Instruction> CompileLVal()
        {
            throw new NotImplementedException();
        }

        private static bool SpecialHandling(BinaryOperator op)
        {
            return op == BinaryOperator.LogicalAnd ||
                   op == BinaryOperator.LogicalOr ||
                   op == BinaryOperator.Coalesce  ||
                   op == BinaryOperator.Assign;
        }
    }
}

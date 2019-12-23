using Redwood.Instructions;
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

        internal override IEnumerable<Instruction> Compile()
        {
            List<Instruction> instructions = new List<Instruction>();
            instructions.AddRange(Left.Compile());
            instructions.Add(Compiler.CompileVariableAssign(TemporaryVariables[0]));
            instructions.AddRange(Right.Compile());
            instructions.Add(Compiler.CompileVariableAssign(TemporaryVariables[1]));

            if ((Left.GetKnownType()?.IsPrimitiveType() ?? false) &&
                (Right.GetKnownType()?.IsPrimitiveType() ?? false))
            {
                Lambda binaryOperationLambda;
                MemberResolver.TryResolveOperator(
                    null,
                    null,
                    Left.GetKnownType(),
                    Right.GetKnownType(),
                    Operator,
                    out binaryOperationLambda);

                if (binaryOperationLambda == null)
                {
                    throw new NotImplementedException();
                }

                // Load and run the lambda on the objects
                instructions.Add(new LoadConstantInstruction(binaryOperationLambda));
                instructions.Add(new InPlaceCallInstruction(new int[]
                    {
                        TemporaryVariables[0].Location,
                        TemporaryVariables[1].Location
                    })
                );
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

        internal override IEnumerable<NameExpression> Walk()
        {
            ReflectedOperatorName = RuntimeUtil.NameForOperator(Operator);
            TemporaryVariables = new List<Variable>();
            if (ReflectedOperatorName == null)
            {
                throw new NotImplementedException();
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

            binder.BindVariable(TemporaryVariables[0]);
            binder.BindVariable(TemporaryVariables[1]);
            Left.Bind(binder);
            Right.Bind(binder);
            
            binder.Checkout();
        }
    }
}

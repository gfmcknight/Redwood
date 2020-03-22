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

        internal Lambda ResolvedOperator { get; set; }
        internal int? ResolvedStaticSlot { get; set; }
        internal int? ResolvedOverloadIndex { get; set; }
        internal bool UsingLeftOperator { get; set; }
        internal RedwoodType LambdaType { get; set; }

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

            int[] argumentLocations = new int[]
            {
                TemporaryVariables[0].Location,
                TemporaryVariables[1].Location
            };

            if (ResolvedOperator != null)
            {
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
            else if (ResolvedStaticSlot != null)
            {
                RedwoodType resolvedOnType = UsingLeftOperator ?
                        Left.GetKnownType() :
                        Right.GetKnownType();

                instructions.Add(
                    new LookupDirectStaticMemberInstruction(
                        resolvedOnType,
                        (int)ResolvedStaticSlot
                    )
                );

                if (ResolvedOverloadIndex != null)
                {
                    instructions.Add(
                        new LookupLambdaGroupOverloadInstruction((int)ResolvedOverloadIndex)
                    );
                }

                if (LambdaType.CSharpType == typeof(InternalLambda))
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
            RedwoodType leftType = Left.GetKnownType();
            RedwoodType rightType = Right.GetKnownType();

            List<Instruction> instructions = new List<Instruction>();
            instructions.AddRange(Right.Compile());

            // If leftType == null, then the left must be responsible for
            // attempting to convert to the correct type
            if (leftType != null)
            {
                instructions.AddRange(
                    Compiler.CompileImplicitConversion(rightType, leftType)
                );
            }

            instructions.AddRange(Left.CompileAssignmentTarget(TemporaryVariables));
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
                        else
                        {
                            // Provide a temporary variable to hold the value of the
                            // assignment where it is needed.
                            TemporaryVariables.Add(new Variable
                            {
                                Temporary = true
                            });
                        }
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

            RedwoodType leftType = Left.GetKnownType();
            RedwoodType rightType = Right.GetKnownType();
            
            if (SpecialHandling(Operator) || leftType == null || rightType == null)
            {
                return;
            }
            else if (leftType.CSharpType != null && rightType.CSharpType != null)
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
                LambdaType = RedwoodType.GetForLambdaArgsTypes(
                    typeof(ExternalLambda),
                    binaryOperationLambda.ReturnType,
                    binaryOperationLambda.ExpectedArgs.ToArray()
                );
            }
            else
            {
                RedwoodType[] lambdaTypes = new RedwoodType[2];
                Lambda[] lambdas = new Lambda[2];
                int?[] slots = new int?[2];
                int?[] indexes = new int?[2];

                // Search both the left and the right types for
                // static lambdas

                lambdaTypes[0] = ResolveBinaryOperator(
                    Operator,
                    leftType,
                    rightType,
                    leftType,
                    out lambdas[0],
                    out slots[0],
                    out indexes[0]
                );

                lambdaTypes[1] = ResolveBinaryOperator(
                    Operator,
                    leftType,
                    rightType,
                    rightType,
                    out lambdas[1],
                    out slots[1],
                    out indexes[1]
                );

                if (lambdaTypes[0] == null && lambdaTypes[1] == null)
                {
                    // There's no compatible operator between the two
                    throw new NotImplementedException();
                }
                else if (lambdaTypes[0] == null)
                {
                    LambdaType = lambdaTypes[1];
                    ResolvedOperator = lambdas[1];
                    ResolvedStaticSlot = slots[1];
                    ResolvedOverloadIndex = indexes[1];
                    UsingLeftOperator = false;
                }
                else if (lambdaTypes[1] == null)
                {
                    LambdaType = lambdaTypes[0];
                    ResolvedOperator = lambdas[0];
                    ResolvedStaticSlot = slots[0];
                    ResolvedOverloadIndex = indexes[0];
                    UsingLeftOperator = true;
                }
                else
                {
                    // TODO: This couldn't return null, could it?
                    RuntimeUtil.TrySelectOverload(
                        new RedwoodType[] { leftType, rightType },
                        lambdaTypes
                           .Select(lambdaType => lambdaType
                                .GenericArguments
                                .SkipLast(1)
                                .ToArray()
                           )
                           .ToArray(),
                        out int res
                     );

                    LambdaType = lambdaTypes[res];
                    ResolvedOperator = lambdas[res];
                    ResolvedStaticSlot = slots[res];
                    ResolvedOverloadIndex = indexes[res];
                    UsingLeftOperator = res == 0;
                }
            }
        }

        private static RedwoodType ResolveBinaryOperator(
            BinaryOperator op,
            RedwoodType left,
            RedwoodType right,
            RedwoodType searchType,
            out Lambda lambda,
            out int? staticSlot,
            out int? index)
        {
            if (searchType.CSharpType == null)
            {
                staticSlot = searchType.staticSlotMap[RuntimeUtil.NameForOperator(op)];
                lambda = null;

                RedwoodType overloadType = searchType.staticSlotTypes[staticSlot ?? -1];
                if (overloadType.CSharpType == typeof(LambdaGroup))
                {
                    if (!RuntimeUtil.TrySelectOverload(
                            new RedwoodType[] { left, right },
                            overloadType.GenericArguments
                                .Select(lambdaType => lambdaType
                                    .GenericArguments
                                    .SkipLast(1)
                                    .ToArray()
                                )
                                .ToArray(),
                            out int resIndex
                        ))
                    {
                        index = null;
                        staticSlot = null;
                        return null;
                    }

                    index = resIndex;
                    return overloadType.GenericArguments[resIndex];
                }

                index = null;
                return overloadType;
            }
            else
            {
                index = null;
                staticSlot = null;
                if (!MemberResolver.TryResolveLambda(
                        searchType,
                        null,
                        RuntimeUtil.NameForOperator(op),
                        out Lambda lambdaOverloads))
                {
                    lambda = null;
                    return null;
                }

                lambda = RuntimeUtil.SelectSingleOverload(
                    new RedwoodType[] { left, right },
                    lambdaOverloads
                );

                return RedwoodType.GetForLambdaArgsTypes(
                    typeof(ExternalLambda),
                    lambda.ReturnType,
                    lambda.ExpectedArgs.ToArray()
                );
            }
        }

        public override RedwoodType GetKnownType()
        {
            if (LambdaType == null || LambdaType.CSharpType == typeof(LambdaGroup))
            {
                return null;
            }

            RedwoodType[] signature = LambdaType.GenericArguments;
            if (signature == null || signature.Length == 0)
            {
                return null;
            }

            return signature[signature.Length - 1];
        }

        internal override IEnumerable<Instruction> CompileAssignmentTarget(
            List<Variable> temporaryVariables)
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

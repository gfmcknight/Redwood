using Redwood.Ast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Redwood.Runtime
{
    internal class OperatorDescriptor
    {
        private int hash;
        private BinaryOperator? binaryOperator;
        private UnaryOperator? unaryOperator;

        public override int GetHashCode()
        {
            return hash;
        }

        public override bool Equals(object obj)
        {
            return obj is OperatorDescriptor od &&
                   od.binaryOperator == binaryOperator &&
                   od.unaryOperator == unaryOperator;
        }

        public OperatorDescriptor(UnaryOperator unaryOperator)
        {
            this.unaryOperator = unaryOperator;
            hash = (int)unaryOperator;
        }
        public OperatorDescriptor(BinaryOperator binaryOperator)
        {
            this.binaryOperator = binaryOperator;
            hash = (int)binaryOperator + Enum.GetNames(typeof(UnaryOperator)).Length;
        }
    }

    internal static partial class MemberResolver
    {
        internal static Dictionary<Type, Dictionary<OperatorDescriptor, Lambda>> primitiveOperators;

        static MemberResolver()
        {
            primitiveOperators = new Dictionary<Type, Dictionary<OperatorDescriptor, Lambda>>();

            // int operators
            RedwoodType type = RedwoodType.GetForCSharpType(typeof(int));
            RedwoodType[] unaryOperatorsType = new RedwoodType[] { type };
            RedwoodType[] binaryOperatorsType = new RedwoodType[] { type, type };


            Dictionary<OperatorDescriptor, Lambda> intOperators =
                new Dictionary<OperatorDescriptor, Lambda>();

            intOperators.Add(
                new OperatorDescriptor(UnaryOperator.BitwiseNegate),
                new InPlaceLambda(unaryOperatorsType, type,
                    (object[] stack, int[] locs) => ~(int)stack[locs[0]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(UnaryOperator.Parity),
                new InPlaceLambda(unaryOperatorsType, type,
                    (object[] stack, int[] locs) => ^(int)stack[locs[0]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(UnaryOperator.Positive),
                new InPlaceLambda(unaryOperatorsType, type,
                    (object[] stack, int[] locs) => +(int)stack[locs[0]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(UnaryOperator.Negative),
                new InPlaceLambda(unaryOperatorsType, type,
                    (object[] stack, int[] locs) => -(int)stack[locs[0]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.Multiply),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] * (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.Divide),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] / (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.Modulus),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] % (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.Add),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] + (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.Subtract),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] - (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.LeftShift),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] << (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.RightShift),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] >> (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.LessThan),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] < (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.GreaterThan),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] > (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.LessThanOrEquals),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] <= (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.GreaterThanOrEquals),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] >= (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.Equals),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] == (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.NotEquals),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] != (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.BitwiseAnd),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] & (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.BitwiseXor),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] ^ (int)stack[locs[1]]
                )
            );

            intOperators.Add(
                new OperatorDescriptor(BinaryOperator.BitwiseOr),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (int)stack[locs[0]] | (int)stack[locs[1]]
                )
            );

            primitiveOperators[typeof(int)] = intOperators;

            // string operators
            type = RedwoodType.GetForCSharpType(typeof(string));

            Dictionary<OperatorDescriptor, Lambda> stringOperators =
                new Dictionary<OperatorDescriptor, Lambda>();

            stringOperators.Add(
                new OperatorDescriptor(BinaryOperator.Add),
                new LambdaGroup(new InPlaceLambda[]
                {
                    new InPlaceLambda(
                        new RedwoodType[] { type, type },
                        type,
                        (object[] stack, int[] locs) => (string)stack[locs[0]] + (string)stack[locs[1]]),
                    new InPlaceLambda(
                        new RedwoodType[] { type, RedwoodType.GetForCSharpType(typeof(int)) },
                        type,
                        (object[] stack, int[] locs) => (string)stack[locs[0]] + (int)stack[locs[1]]),
                    new InPlaceLambda(
                        new RedwoodType[] { type, RedwoodType.GetForCSharpType(typeof(double)) },
                        type,
                        (object[] stack, int[] locs) => (string)stack[locs[0]] + (double)stack[locs[1]]),
                    new InPlaceLambda(
                        new RedwoodType[] { type, RedwoodType.GetForCSharpType(typeof(bool)) },
                        type,
                        (object[] stack, int[] locs) => (string)stack[locs[0]] + (bool)stack[locs[1]])
                })
            );

            primitiveOperators[typeof(string)] = stringOperators;

            type = RedwoodType.GetForCSharpType(typeof(double));
            unaryOperatorsType = new RedwoodType[] { type };
            binaryOperatorsType = new RedwoodType[] { type, type };

            Dictionary<OperatorDescriptor, Lambda> doubleOperators =
                new Dictionary<OperatorDescriptor, Lambda>();

            doubleOperators.Add(
                new OperatorDescriptor(UnaryOperator.Positive),
                new InPlaceLambda(unaryOperatorsType, type,
                    (object[] stack, int[] locs) => +(double)stack[locs[0]]
                )
            );

            doubleOperators.Add(
                new OperatorDescriptor(UnaryOperator.Negative),
                new InPlaceLambda(unaryOperatorsType, type,
                    (object[] stack, int[] locs) => -(double)stack[locs[0]]
                )
            );

            doubleOperators.Add(
                new OperatorDescriptor(BinaryOperator.Multiply),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (double)stack[locs[0]] * (double)stack[locs[1]]
                )
            );

            doubleOperators.Add(
                new OperatorDescriptor(BinaryOperator.Divide),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (double)stack[locs[0]] / (double)stack[locs[1]]
                )
            );

            doubleOperators.Add(
                new OperatorDescriptor(BinaryOperator.Modulus),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (double)stack[locs[0]] % (double)stack[locs[1]]
                )
            );

            doubleOperators.Add(
                new OperatorDescriptor(BinaryOperator.Add),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (double)stack[locs[0]] + (double)stack[locs[1]]
                )
            );

            doubleOperators.Add(
                new OperatorDescriptor(BinaryOperator.Subtract),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (double)stack[locs[0]] - (double)stack[locs[1]]
                )
            );

            doubleOperators.Add(
                new OperatorDescriptor(BinaryOperator.LessThan),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (double)stack[locs[0]] < (double)stack[locs[1]]
                )
            );

            doubleOperators.Add(
                new OperatorDescriptor(BinaryOperator.GreaterThan),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (double)stack[locs[0]] > (double)stack[locs[1]]
                )
            );

            doubleOperators.Add(
                new OperatorDescriptor(BinaryOperator.LessThanOrEquals),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (double)stack[locs[0]] <= (double)stack[locs[1]]
                )
            );

            doubleOperators.Add(
                new OperatorDescriptor(BinaryOperator.GreaterThanOrEquals),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (double)stack[locs[0]] >= (double)stack[locs[1]]
                )
            );

            doubleOperators.Add(
                new OperatorDescriptor(BinaryOperator.Equals),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (double)stack[locs[0]] == (double)stack[locs[1]]
                )
            );

            doubleOperators.Add(
                new OperatorDescriptor(BinaryOperator.NotEquals),
                new InPlaceLambda(binaryOperatorsType, type,
                    (object[] stack, int[] locs) => (double)stack[locs[0]] != (double)stack[locs[1]]
                )
            );

            primitiveOperators[typeof(double)] = doubleOperators;

            Dictionary<OperatorDescriptor, Lambda> boolOperators =
                new Dictionary<OperatorDescriptor, Lambda>();
            primitiveOperators[typeof(bool)] = boolOperators;
        }
    }
}
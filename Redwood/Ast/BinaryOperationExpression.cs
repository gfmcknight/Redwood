using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Ast
{
    public enum BinaryOperator
    {
        Add,
        Subtract,
        Multiply,
        Divide,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,
        Equal,
        NotEqual,
        LogicalAnd,
        LogicalOr,
        BitwiseAnd,
        BitwiseOr,
        BitwiseExclusiveOr
    }
    public class BinaryOperationExpression
    {
        public Expression Left { get; set; }
        public Expression Right { get; set; }
    }
}

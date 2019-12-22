using Redwood.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Ast
{
    public enum UnaryOperator
    {
        PostIncrement,
        PreIncrement,
        PostDecrement,
        PreDecrement,
        Negative,
        Positive,
        BitwiseNegate,
        Parity,
        Await
    }
    public class UnaryOperationExpression : Expression
    {
        public UnaryOperator Operator { get; set; }
        public Expression InnerExpression { get; set; }

        internal override void Bind(Binder binder)
        {
            throw new NotImplementedException();
        }

        internal override IEnumerable<Instruction> Compile()
        {
            throw new NotImplementedException();
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            throw new NotImplementedException();
        }
    }
}

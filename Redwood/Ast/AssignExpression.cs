using System;
using System.Collections.Generic;
using System.Text;
using Redwood.Instructions;

namespace Redwood.Ast
{
    public class AssignExpression : Expression
    {
        public Expression Left { get; set; }
        public Expression Right { get; set; }

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
            List<NameExpression> freeVariables = new List<NameExpression>();
            freeVariables.AddRange(Left.Walk());
            freeVariables.AddRange(Right.Walk());
            return freeVariables;
        }
    }
}

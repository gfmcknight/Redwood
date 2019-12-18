using System;
using System.Collections.Generic;
using System.Text;
using Redwood.Instructions;

namespace Redwood.Ast
{
    public class DotWalkExpression : Expression
    {
        public Expression Chain { get; set; }
        public NameExpression Element { get; set; }

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

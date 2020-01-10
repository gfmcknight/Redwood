using Redwood.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Ast
{
    public class NullExpression : Expression
    {
        internal override void Bind(Binder binder)
        {
            // Nothing to bind
        }

        internal override IEnumerable<Instruction> Compile()
        {
            return new Instruction[]
            {
                new LoadConstantInstruction(null)
            };
        }

        internal override IEnumerable<Instruction> CompileLVal()
        {
            throw new NotImplementedException();
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            return new NameExpression[0];
        }
    }
}

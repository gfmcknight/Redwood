using System;
using System.Collections.Generic;
using System.Text;
using Redwood.Instructions;

namespace Redwood.Ast
{
    public class LetDefinition : Definition
    {
        public Expression Initializer { get; set; }

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
            base.Walk();
            DeclaredVariable.DefinedConstant = Initializer.Constant;
            return Initializer?.Walk() ?? new NameExpression[0];
        }
    }
}

using Redwood.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Ast
{
    class ImportDefinition : Definition
    {
        internal override void Bind(Binder binder)
        {
            throw new NotImplementedException();
        }

        internal override IEnumerable<Instruction> Compile()
        {
            throw new NotImplementedException();
        }
    }
}

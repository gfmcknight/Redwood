using System;
using System.Collections.Generic;
using System.Text;
using Redwood.Instructions;
using Redwood.Runtime;

namespace Redwood.Ast
{
    public class ClassDefinition : Definition
    {
        public IEnumerable<LetDefinition> Fields { get; set; }
        public IEnumerable<FunctionDefinition> Constructors { get; set; }
        public IEnumerable<FunctionDefinition> Methods { get; set; }
        internal RedwoodType Type { get; set; }

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

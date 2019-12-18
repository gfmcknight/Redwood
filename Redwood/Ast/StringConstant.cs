using Redwood.Instructions;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Ast
{
    public class StringConstant : Expression
    {
        public string Value { get; set; }

        public override RedwoodType GetKnownType()
        {
            return RedwoodType.GetForCSharpType(typeof(string));
        }

        internal override void Bind(Binder binder)
        {
            return;
        }

        internal override IEnumerable<Instruction> Compile()
        {
            return new Instruction[]
            {
                new LoadConstantInstruction(Value)
            };
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            return new NameExpression[0];
        }
    }
}

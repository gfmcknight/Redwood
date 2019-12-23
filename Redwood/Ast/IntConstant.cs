using Redwood.Instructions;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Redwood.Ast
{
    public class IntConstant : Expression
    {
        public BigInteger Value { get; set; }
        public override bool Constant { get; } = true;

        public override RedwoodType GetKnownType()
        {
            if (Value < int.MaxValue && Value > int.MinValue)
            {
                return RedwoodType.GetForCSharpType(typeof(int));
            }
            return RedwoodType.GetForCSharpType(typeof(BigInteger));
        }

        public override object EvaluateConstant()
        {
            return Value;
        }

        internal override void Bind(Binder binder)
        {
            return;
        }

        internal override IEnumerable<Instruction> Compile()
        {
            object value;

            if (Value < int.MaxValue && Value > int.MinValue)
            {
                value = (int)Value;
            }
            else
            {
                value = Value;
            }

            return new Instruction[]
            {
                new LoadConstantInstruction(value)
            };
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            return new NameExpression[0];
        }
    }
}

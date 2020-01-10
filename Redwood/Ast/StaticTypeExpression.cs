using Redwood.Instructions;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Ast
{
    public class StaticTypeExpression : Expression
    {
        public Expression Expression { get; set; }

        internal override void Bind(Binder binder)
        {
            Expression.Bind(binder);
        }

        internal override IEnumerable<Instruction> Compile()
        {
            return new Instruction[]
            {
                new LoadConstantInstruction(Expression.GetKnownType())
            };
        }

        internal override IEnumerable<Instruction> CompileLVal()
        {
            throw new NotImplementedException();
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            return Expression.Walk();
        }

        public override RedwoodType GetKnownType()
        {
            return RedwoodType.GetForCSharpType(typeof(RedwoodType));
        }
    }
}

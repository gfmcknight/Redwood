using Redwood.Instructions;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Ast
{
    public class ReturnStatement : Statement
    {
        public Expression Expression { get; set; }
        internal RedwoodType ReturnType { get; set; }

        internal override void Bind(Binder binder)
        {
            ReturnType = binder.GetReturnType();
            Expression.Bind(binder);
            return;
        }

        internal override IEnumerable<Instruction> Compile()
        {
            List<Instruction> instructions = new List<Instruction>();
            if (Expression != null)
            {
                instructions.AddRange(Expression.Compile());
                if (ReturnType != Expression.GetKnownType() && ReturnType != null)
                {
                    instructions.AddRange(
                        Compiler.CompileImplicitConversion(Expression.GetKnownType(), ReturnType)
                    );
                }
            }
            instructions.Add(new ReturnInstruction());
            return instructions;
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            return Expression.Walk();
        }
    }
}

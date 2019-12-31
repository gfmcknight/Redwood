using System;
using System.Collections.Generic;
using System.Text;
using Redwood.Instructions;
using Redwood.Runtime;

namespace Redwood.Ast
{
    public class NameExpression : Expression
    {
        public string Name { get; set; }
        internal bool RequiresClosure { get; set; }
        internal bool InLVal { get; set; }
        internal Variable Variable { get; set; }

        internal override void Bind(Binder binder)
        {
            return;
        }

        internal override IEnumerable<Instruction> Compile()
        {
            return new Instruction[]
            {
                Compiler.CompileVariableLookup(Variable)
            };
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            return new NameExpression[] { this };
        }

        public override RedwoodType GetKnownType()
        {
            return Variable?.KnownType;
        }

        internal override IEnumerable<Instruction> CompileLVal()
        {
            throw new NotImplementedException();
        }
    }
}

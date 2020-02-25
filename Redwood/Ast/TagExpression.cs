using Redwood.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Ast
{
    public class TagExpression : Expression
    {
        public StringConstant Tag { get; set; }

        internal override void Bind(Binder binder)
        {
            // Nothing to bind
        }

        internal override IEnumerable<Instruction> Compile()
        {
            return new Instruction[] { new TagInstruction(Tag.Value) };
        }

        internal override IEnumerable<Instruction> CompileAssignmentTarget(
            List<Variable> temporaryVariables)
        {
            throw new NotImplementedException();
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            return new NameExpression[0];
        }
    }
}

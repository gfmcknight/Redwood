using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Redwood.Instructions;
using Redwood.Runtime;

namespace Redwood.Ast
{
    public class IfStatement : Statement
    {
        public Expression Condition { get; set; }
        public Statement PathTrue { get; set; }
        public Statement ElseStatement { get; set; }

        internal override void Bind(Binder binder)
        {
            throw new NotImplementedException();
        }

        internal override IEnumerable<Instruction> Compile()
        {
            IEnumerable<Instruction> compiledCondition = Condition.Compile();
            Instruction[] compiledPathTrue = PathTrue.Compile().ToArray();
            Instruction[] compiledElseStatement = ElseStatement.Compile().ToArray();

            List<Instruction> instructions = new List<Instruction>();
            instructions.AddRange(compiledCondition);
            if (RedwoodType.GetForCSharpType(typeof(bool)) != Condition.GetKnownType())
            {
                throw new NotImplementedException();
            }

            instructions.Add(new ConditionalJumpInstruction(compiledPathTrue.Length + 1));
            instructions.AddRange(compiledPathTrue);
            if (ElseStatement != null)
            {
                instructions.Add(new JumpInstruction(compiledElseStatement.Length));
                instructions.AddRange(compiledElseStatement);
            }

            return instructions;
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            throw new NotImplementedException();
        }
    }
}

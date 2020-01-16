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
            Condition.Bind(binder);
            
            binder.Bookmark();
            PathTrue.Bind(binder);
            binder.Checkout();

            if (ElseStatement != null)
            {
                binder.Bookmark();
                ElseStatement.Bind(binder);
                binder.Checkout();
            }
        }

        internal override IEnumerable<Instruction> Compile()
        {
            IEnumerable<Instruction> compiledCondition = Condition.Compile();
            Instruction[] compiledPathTrue = PathTrue.Compile().ToArray();
            Instruction[] compiledElseStatement = ElseStatement?.Compile().ToArray() ?? new Instruction[0];

            List<Instruction> instructions = new List<Instruction>();
            instructions.AddRange(compiledCondition);
            if (RedwoodType.GetForCSharpType(typeof(bool)) != Condition.GetKnownType())
            {
                throw new NotImplementedException();
            }

            // The index of the jump instruction relative to the the else is -1
            // (ie. right before the start). When we factor in the jump, we need
            // to skip 2 additional instructions
            instructions.Add(new ConditionalJumpInstruction(compiledElseStatement.Length + 2));
            instructions.AddRange(compiledElseStatement);
            instructions.Add(new JumpInstruction(compiledPathTrue.Length + 1));
            instructions.AddRange(compiledPathTrue);

            return instructions;
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            List<NameExpression> freeVars = new List<NameExpression>();
            freeVars.AddRange(Condition.Walk());
            freeVars.AddRange(PathTrue.Walk());
            if (ElseStatement != null)
            {
                freeVars.AddRange(ElseStatement.Walk());
            }
            return freeVars;
        }
    }
}

using Redwood.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Ast
{
    public class TopLevel : Statement
    {
        public Definition[] Definitions { get; set; }

        internal override void Bind(Binder binder)
        {
            foreach (Definition definition in Definitions)
            {
                definition.Bind(binder);
            }
        }

        internal override IEnumerable<Instruction> Compile()
        {
            List<Instruction> instructions = new List<Instruction>();
            foreach (Definition definition in Definitions)
            {
                instructions.AddRange(definition.Compile());
            }
            instructions.Add(new ReturnInstruction());
            return instructions;
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            List<NameExpression> freeVars = new List<NameExpression>();
            foreach (Definition definition in Definitions)
            {
                freeVars.AddRange(definition.Walk());
            }

            List<Variable> definedVariables = new List<Variable>();
            foreach (Definition definition in Definitions)
            {
                definition.DeclaredVariable.Global = true;
                definedVariables.Add(definition.DeclaredVariable);
            }

            Compiler.MatchVariables(freeVars, definedVariables);
            return freeVars;
        }
    }
}

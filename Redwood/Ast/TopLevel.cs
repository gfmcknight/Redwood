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
            // First, bind in order to set up the types of all lambdas
            foreach (Definition definition in Definitions)
            {
                definition.Bind(new Binder());
            }

            // TODO: Bind all included modules here
            
            // Now that the types of all functions are known, we can bind again
            // overwriting all variable positions
            foreach (Definition definition in Definitions)
            {
                definition.Bind(binder);
            }

            // TODO: Bind all included modules again
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

            // TODO: Walk the imports that refer to actual Redwood modules?
        }
    }
}

using Redwood.Instructions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Redwood.Ast
{
    public class TopLevel : Statement
    {
        public Definition[] Definitions { get; set; }

        internal List<OverloadGroup> Overloads { get; set; }

        internal override void Bind(Binder binder)
        {
            foreach (OverloadGroup overload in Overloads)
            {
                overload.DoBind(new Binder());
            }
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
            foreach (OverloadGroup overload in Overloads)
            {
                overload.DoBind(binder);
            }
            // TODO: Bind all included modules again
        }

        internal override IEnumerable<Instruction> Compile()
        {
            List<Instruction> instructions = new List<Instruction>();

            foreach (OverloadGroup overload in Overloads)
            {
                instructions.AddRange(overload.Compile());
            }

            foreach (Definition definition in Definitions)
            {
                if (definition is FunctionDefinition)
                {
                    continue;
                }
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
                if (!(definition is FunctionDefinition))
                {
                    definedVariables.Add(definition.DeclaredVariable);
                }
            }

            List<FunctionDefinition> functions = Definitions
                .Where(statement => statement is FunctionDefinition)
                .Select(statement => statement as FunctionDefinition)
                .ToList();
            Overloads = Compiler.GenerateOverloads(functions);
            definedVariables.AddRange(Overloads.Select(o => o.variable));

            foreach (Variable variable in definedVariables)
            {
                variable.Global = true;
            }

            Compiler.MatchVariables(freeVars, definedVariables);
            return freeVars;

            // TODO: Walk the imports that refer to actual Redwood modules?
        }
    }
}

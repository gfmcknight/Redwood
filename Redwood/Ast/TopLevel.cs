using Redwood.Instructions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Redwood.Ast
{
    public class TopLevel : Statement
    {
        public string ModuleName { get; set; }
        public Definition[] Definitions { get; set; }

        internal List<Variable> DeclaredVariables { get; set; }
        internal List<OverloadGroup> Overloads { get; set; }
        internal int StackSize { get; set; }

        internal override void Bind(Binder binder)
        {
            binder.EnterFullScope();
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
            StackSize = binder.LeaveFullScope();
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

            DeclaredVariables = new List<Variable>();
            foreach (Definition definition in Definitions)
            {
                if (!(definition is FunctionDefinition))
                {
                    DeclaredVariables.Add(definition.DeclaredVariable);
                }
            }

            List<FunctionDefinition> functions = Definitions
                .Where(statement => statement is FunctionDefinition)
                .Select(statement => statement as FunctionDefinition)
                .ToList();
            Overloads = Compiler.GenerateOverloads(functions);
            DeclaredVariables.AddRange(Overloads.Select(o => o.variable));

            foreach (Variable variable in DeclaredVariables)
            {
                variable.Global = true;
            }

            Compiler.MatchVariables(freeVars, DeclaredVariables);
            return freeVars;

            // TODO: Walk the imports that refer to actual Redwood modules?
        }
    }
}

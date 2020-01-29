using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Redwood.Instructions;
using Redwood.Runtime;

namespace Redwood.Ast
{
    public class BlockStatement : Statement
    {
        public Statement[] Statements { get; set; }

        internal List<Variable> DeclaredVariables { get; set; }

        internal List<OverloadGroup> Overloads { get; set; }


        internal override void Bind(Binder binder)
        {
            foreach (Statement statement in Statements.Where(s => s is FunctionDefinition))
            {
                statement.Bind(binder);
            }

            // Overloads must be bound before temporary variables or else they
            // will get overwritten, but after function definitions in order to
            // know their own type
            foreach (OverloadGroup overload in Overloads)
            {
                overload.DoBind(binder);
            }

            foreach (Statement statement in Statements.Where(s => !(s is FunctionDefinition)))
            {
                statement.Bind(binder);
            }
        }

        internal override IEnumerable<Instruction> Compile()
        {
            List<Instruction> instructions = new List<Instruction>();

            foreach (OverloadGroup group in Overloads)
            {
                instructions.AddRange(group.Compile());
            }

            foreach (Statement statement in Statements)
            {
                // Special handling of function definitions so that
                // we can deduplicate overloads
                if (statement is FunctionDefinition)
                {
                    continue;
                }
                instructions.AddRange(statement.Compile());
            }
            return instructions;
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            List<NameExpression> freeVariables = new List<NameExpression>();
            foreach (Statement statement in Statements)
            {
                freeVariables.AddRange(statement.Walk());
            }


            // The Walk() call will have populated all variables in LetStatements
            // For function definitions, we need a special handling for overloads
            DeclaredVariables = Statements
                .Where(statement => statement is Definition
                       && !(statement is FunctionDefinition))
                .Select(statement => (statement as Definition).DeclaredVariable)
                .ToList();

            List<FunctionDefinition> functions = Statements
                .Where(statement => statement is FunctionDefinition)
                .Select(statement => statement as FunctionDefinition)
                .ToList();
            Overloads = Compiler.GenerateOverloads(functions);
            DeclaredVariables.AddRange(Overloads.Select(o => o.variable));

            Compiler.MatchVariables(freeVariables, DeclaredVariables);

            return freeVariables;
        }
    }
}

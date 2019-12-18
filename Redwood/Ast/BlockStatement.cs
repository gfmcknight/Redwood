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

        internal IEnumerable<Variable> DeclaredVariables { get; set; }

        internal override void Bind(Binder binder)
        {
            foreach (Statement statement in Statements)
            {
                statement.Bind(binder);
            }
        }

        internal override IEnumerable<Instruction> Compile()
        {
            List<Instruction> instructions = new List<Instruction>();
            foreach (Statement statement in Statements)
            {
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
            DeclaredVariables = Statements
                .Where(statement => statement is Definition)
                .Select(statement => (statement as Definition).DeclaredVariable);

            Compiler.MatchVariables(freeVariables, DeclaredVariables);

            return freeVariables;
        }
    }
}

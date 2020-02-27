using Redwood.Instructions;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Redwood.Ast
{
    class ForStatement : Statement
    {
        public Statement Initializer { get; set; }
        public Expression Condition { get; set; }
        public Expression Incrementor { get; set; }
        public BlockStatement Body { get; set; }

        internal override IEnumerable<NameExpression> Walk()
        {
            List<NameExpression> freeVars = new List<NameExpression>();
            freeVars.AddRange(Initializer?.Walk() ?? new NameExpression[0]);
            freeVars.AddRange(Condition?.Walk() ?? new NameExpression[0]);
            freeVars.AddRange(Incrementor?.Walk() ?? new NameExpression[0]);
            freeVars.AddRange(Body.Walk());

            if (Initializer is Definition definition)
            {
                List<Variable> initializerVariable = new List<Variable> 
                {
                    definition.DeclaredVariable
                };

                Compiler.MatchVariables(freeVars, initializerVariable);
            }

            return freeVars;
        }

        internal override void Bind(Binder binder)
        {
            binder.Bookmark();

            Initializer?.Bind(binder);
            Condition?.Bind(binder);
            Body.Bind(binder);
            Incrementor?.Bind(binder);

            binder.Checkout();
        }

        internal override IEnumerable<Instruction> Compile()
        {

            List<Instruction> instructions = new List<Instruction>();

            instructions.AddRange(Initializer?.Compile() ?? new Instruction[0]);

            List<Instruction> bodyInstructions = Body.Compile().ToList();
            bodyInstructions.AddRange(Incrementor?.Compile() ?? new Instruction[0]);



            List<Instruction> check;
            if (Condition == null)
            {
                // No condition? We're in a forever loop
                check = new List<Instruction>();
            }
            else
            {
                check = Condition.Compile().ToList();
                check.AddRange(
                    Compiler.CompileImplicitConversion(
                        Condition.GetKnownType(),
                        RedwoodType.GetForCSharpType(typeof(bool))
                    )
                );
                check.Add(new ConditionalJumpInstruction(2));
                // Next instruction (1) + Body + Increment + Jump instruction (1)
                check.Add(new JumpInstruction(bodyInstructions.Count + 2));
            }

            // Jump back to the beginning of the loop
            bodyInstructions.Add(new JumpInstruction(-(bodyInstructions.Count + check.Count)));

            instructions.AddRange(check);
            instructions.AddRange(bodyInstructions);
            return instructions;
        }
    }
}

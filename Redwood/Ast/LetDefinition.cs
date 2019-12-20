using System;
using System.Collections.Generic;
using System.Text;
using Redwood.Instructions;

namespace Redwood.Ast
{
    public class LetDefinition : Definition
    {
        public Expression Initializer { get; set; }
        public TypeSyntax Type { get; set; }

        internal override void Bind(Binder binder)
        {
            base.Bind(binder);
            if (Initializer != null)
            {
                Initializer.Bind(binder);
            }
        }

        internal override IEnumerable<Instruction> Compile()
        {
            List<Instruction> instructions = new List<Instruction>();
            if (Initializer == null)
            {
                instructions.Add(new LoadConstantInstruction(null));
            }
            else
            {
                instructions.AddRange(Initializer.Compile());
            }

            instructions.Add(Compiler.CompileVariableAssign(DeclaredVariable));
            return instructions;
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            base.Walk(); // TODO: will we ever need the result of this?
            DeclaredVariable.DefinedConstant = Initializer.Constant;
            return Initializer?.Walk() ?? new NameExpression[0];
        }
    }
}

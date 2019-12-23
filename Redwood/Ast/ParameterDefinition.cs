using Redwood.Instructions;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Redwood.Ast
{
    public class ParameterDefinition : Definition
    {
        public TypeSyntax Type { get; set; }

        internal Variable StackInputVariable { get; set; }

        internal override IEnumerable<NameExpression> Walk()
        {
            List<NameExpression> freeVars = base.Walk().ToList();
            freeVars.AddRange(Type.Walk());
            return freeVars;
        }

        internal override void Bind(Binder binder)
        {
            base.Bind(binder);

            if (RedwoodType.TryGetPrimitiveFromName(Type.TypeName.Name, out RedwoodType type))
            {
                DeclaredVariable.KnownType = type;
            }
            else if (Type.TypeName.Variable.DefinedConstant && !Type.TypeName.Variable.Mutated)
            {
                DeclaredVariable.KnownType = Type.TypeName.Variable.ConstantValue as RedwoodType;
            }

            // In the case that we have a closured variable, we still need
            // to take the input on the stack, so we need a temporary variable
            // to this end.
            if (DeclaredVariable.Closured)
            {
                StackInputVariable = new Variable
                {
                    KnownType = DeclaredVariable.KnownType,
                    Temporary = true
                };
                binder.BindVariable(StackInputVariable);
            }
        }

        internal override IEnumerable<Instruction> Compile()
        {
            if (StackInputVariable == null)
            {
                return new Instruction[0];
            }

            return new Instruction[]
            {
                Compiler.CompileVariableLookup(StackInputVariable),
                Compiler.CompileVariableAssign(DeclaredVariable)
            };
        }
    }
}

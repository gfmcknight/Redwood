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

        internal override IEnumerable<NameExpression> Walk()
        {
            List<NameExpression> freeVars = base.Walk().ToList();
            freeVars.AddRange(Type.Walk());
            return freeVars;
        }

        internal override void Bind(Binder binder)
        {
            binder.BindVariable(DeclaredVariable);

            if (RedwoodType.TryGetPrimitiveFromName(Type.TypeName.Name, out RedwoodType type))
            {
                DeclaredVariable.KnownType = type;
            }
            else
            {
                throw new NotImplementedException("Non-primitive types");
            }
        }

        internal override IEnumerable<Instruction> Compile()
        {
            throw new NotImplementedException();
        }
    }
}

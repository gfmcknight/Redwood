using Redwood.Instructions;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Ast
{
    public class TypeSyntax : Statement
    {
        public NameExpression TypeName { get; set; }
        public IEnumerable<TypeSyntax> GenericInnerTypes { get; set; }

        internal override void Bind(Binder binder)
        {
            throw new NotImplementedException();
        }

        internal override IEnumerable<Instruction> Compile()
        {
            throw new NotImplementedException();
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            // TODO: Walk the GenericInnerTypes
            if (RedwoodType.TryGetPrimitiveFromName(TypeName.Name, out RedwoodType type))
            {
                return new NameExpression[0];
            }
            return new NameExpression[] { TypeName };
        }

        internal RedwoodType GetIndicatedType()
        {
            if (RedwoodType.TryGetPrimitiveFromName(TypeName.Name, out RedwoodType type))
            {
                return type;
            }
            return TypeName.Variable.KnownType;
        }
    }
}

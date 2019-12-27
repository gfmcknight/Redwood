using Redwood.Instructions;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Redwood.Ast
{
    public class TypeSyntax : Statement
    {
        public NameExpression TypeName { get; set; }
        public TypeSyntax[] GenericInnerTypes { get; set; }

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
            RedwoodType knownType = TypeName.GetKnownType();
            if (GenericInnerTypes == null)
            {
                return knownType;
            }
            else if (knownType.CSharpType == null)
            {
                // TODO: Just compile dynamic checks where necessary?
                throw new NotImplementedException();
            }
            else
            {
                knownType.CSharpType.MakeGenericType(
                    GenericInnerTypes.Select(typeSyntax =>
                    {
                        RedwoodType genericType = typeSyntax.GetIndicatedType();
                        // TODO?
                        if (genericType == null)
                        {
                            return typeof(object);
                        }
                        // Definitely TODO... I guess this will just happen
                        // with dynamic checks everywhere?
                        if (genericType.CSharpType == null)
                        {
                            return typeof(RedwoodObject);
                        }
                        return genericType.CSharpType;
                    }).ToArray()
                );
            }
            return TypeName.GetKnownType();
        }
    }
}

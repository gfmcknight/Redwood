using Redwood.Instructions;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Redwood.Ast
{
    class ImportDefinition : Definition
    {
        private DotWalkExpression namespaceWalk;
        public DotWalkExpression NamespaceWalk {
            get
            {
                return namespaceWalk;
            }
            set
            {
                namespaceWalk = value;
                Name = namespaceWalk.Element.Name;
            }
        }

        internal override void Bind(Binder binder)
        {
            base.Bind(binder);
            DeclaredVariable.KnownType = RedwoodType.GetForCSharpType(typeof(RedwoodType));
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            base.Walk();
            DeclaredVariable.DefinedConstant = true;
            DeclaredVariable.KnownType = RedwoodType.GetForCSharpType(typeof(RedwoodType));
            string typename = CollectName(namespaceWalk);

            if (TryGetTypeFromAssemblies(typename, out Type cSharpType))
            {
                DeclaredVariable.ConstantValue = RedwoodType.GetForCSharpType(cSharpType);
            }
            else
            {
                // TODO: Cross module imports
                throw new NotImplementedException();
            }
            return new NameExpression[0];
        }

        private bool TryGetTypeFromAssemblies(string name, out Type type)
        {
            foreach (Assembly assembly in Compiler.assemblies)
            {
                type = assembly.GetType(name, false);
                if (type != null)
                {
                    return true;
                }
            }
            type = null;
            return false;
        }

        private string CollectName(Expression e)
        {
            if (e is DotWalkExpression dwe)
            {
                return CollectName(dwe.Chain) + "." + dwe.Element.Name;
            }
            else if (e is NameExpression ne)
            {
                return ne.Name;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        internal override IEnumerable<Instruction> Compile()
        {
            // Because we declared it constant, there should be no
            // reason to dedicate a variable to this
            return new Instruction[0];
        }
    }
}

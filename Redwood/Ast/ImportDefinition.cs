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

        internal NameExpression FreeVar { get; set; }

        internal override void Bind(Binder binder)
        {
            base.Bind(binder);
            if (FreeVar != null)
            {
                DeclaredVariable.KnownType = FreeVar.GetKnownType();
                DeclaredVariable.DefinedConstant = FreeVar.Variable.DefinedConstant;
                DeclaredVariable.ConstantValue = FreeVar.Variable.ConstantValue;
            }
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            base.Walk();
            string typename = CollectName(namespaceWalk);

            if (TryGetTypeFromAssemblies(typename, out Type cSharpType))
            {
                DeclaredVariable.DefinedConstant = true;
                DeclaredVariable.KnownType = RedwoodType.GetForCSharpType(typeof(RedwoodType));
                DeclaredVariable.ConstantValue = RedwoodType.GetForCSharpType(cSharpType);
            }
            else
            {
                FreeVar = new NameExpression { Name = typename };
                // Let the compiler take it because it needs to go parse
                // these additional modules
                return new NameExpression[] { FreeVar };
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

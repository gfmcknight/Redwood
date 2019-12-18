using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Ast
{
    public abstract class Definition : Statement
    {
        public string Name { get; set; }
        internal Variable DeclaredVariable { get; set; }

        internal override void Bind(Binder binder)
        {
            binder.BindVariable(DeclaredVariable);
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            DeclaredVariable = new Variable
            {
                Name = Name
            };
            return new NameExpression[0];
        }
    }
}

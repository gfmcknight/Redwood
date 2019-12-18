using Redwood.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Ast
{
    public abstract class Statement
    {
        public Statement Parent { get; set; }

        internal abstract IEnumerable<Instruction> Compile();
        
        /// <summary>
        /// Walks the AST and matches all variables to their definitions.
        /// </summary>
        /// <returns>The set of free variables in the Statement.</returns>
        internal abstract IEnumerable<NameExpression> Walk();

        /// <summary>
        /// Walks the AST and binds types expressions and resolves
        /// overloads, when possible.
        /// </summary>
        internal abstract void Bind(Binder binder);

        // Information about where this expression is
        // in the code
        public int LineNumberStart { get; set; }
        public int LineNumberEnd { get; set; }
        public int IndexStart { get; set; }
        public int IndexEnd { get; set; }
    }
}

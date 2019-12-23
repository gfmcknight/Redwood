using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood
{
    internal class Variable
    {
        internal string Name { get; set; }
        internal bool Temporary { get; set; }
        internal bool DefinedConstant { get; set; }
        internal object ConstantValue { get; set; }
        internal bool Mutated { get; set; }
        internal RedwoodType KnownType { get; set; }
        internal bool Global { get; set; }
        internal int Location { get; set; }
        internal bool Closured { get; set; }
        internal int ClosureID { get; set; }
    }
}

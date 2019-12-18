using Redwood.Instructions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Runtime
{
    internal class Frame
    {
        internal object result;
        internal object[] stack;
        internal Closure[] closures;
        internal GlobalContext global;
        internal Instruction[] instructions;
        internal bool shouldExit;
        internal int returnAddress;
        internal bool shouldCall;

        internal Frame(GlobalContext context, InternalLambda lambda)
        {
            this.global = context;
            this.closures = lambda.closures;
            this.stack = new object[lambda.description.stackSize];
            this.instructions = lambda.description.instructions;
        }
    }
}

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
            global = context;
            closures = new Closure[lambda.closures.Length + 1];
            int i;
            for (i = 0; i < lambda.closures.Length; i++)
            {
                closures[i] = lambda.closures[i];
            }
            closures[i] = new Closure(this, lambda.description.closureSize);
                
            stack = new object[lambda.description.stackSize];
            instructions = lambda.description.instructions;
        }
    }
}

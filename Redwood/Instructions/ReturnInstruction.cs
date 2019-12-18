using System;
using System.Collections.Generic;
using System.Text;
using Redwood.Runtime;

namespace Redwood.Instructions
{
    internal class ReturnInstruction : Instruction
    {
        public int Execute(Frame frame)
        {
            frame.shouldExit = true;
            return 1;
        }
    }
}

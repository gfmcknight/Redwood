using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Instructions
{
    internal class LoadConstantInstruction : Instruction
    {
        private object constant;
        internal LoadConstantInstruction(object constant)
        {
            this.constant = constant;
        }
        public int Execute(Frame frame)
        {
            frame.result = constant;
            return 1;
        }
    }
}

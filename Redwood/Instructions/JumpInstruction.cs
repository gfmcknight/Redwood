using System;
using System.Collections.Generic;
using System.Text;
using Redwood.Runtime;

namespace Redwood.Instructions
{
    internal class JumpInstruction : Instruction
    {
        private int amount;

        public JumpInstruction(int amount)
        {
            this.amount = amount;
        }

        public int Execute(Frame frame)
        {
            return amount;
        }
    }
    internal class ConditionalJumpInstruction : Instruction
    {
        private int amount;

        public ConditionalJumpInstruction(int amount)
        {
            this.amount = amount;
        }

        public int Execute(Frame frame)
        {
            // TODO: do we assume that this is only called after
            // the value has already been correctly converted?
            if ((bool)frame.result)
            {
                return amount;
            }

            return 1;
        }
    }
}

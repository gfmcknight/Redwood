using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Instructions
{
    internal class AssignLocalInstruction : Instruction
    {
        private int index;
        public AssignLocalInstruction(int index)
        {
            this.index = index;
        }
        public int Execute(Frame frame)
        {
            frame.stack[index] = frame.result;
            return 1;
        }
    }

    internal class AssignClosureInstruction : Instruction
    {
        private int closureId;
        private int index;

        public AssignClosureInstruction(int closureId, int index)
        {
            this.closureId = closureId;
            this.index = index;
        }

        public int Execute(Frame frame)
        {
            frame.closures[closureId].data[index] = frame.result;
            return 1;
        }
    }

    internal class AssignGlobalInstruction : Instruction
    {
        private string name;

        public AssignGlobalInstruction(string name)
        {
            this.name = name;
        }

        public int Execute(Frame frame)
        {
            frame.global.AssignVariable(name, frame.result);
            return 1;
        }
    }
}

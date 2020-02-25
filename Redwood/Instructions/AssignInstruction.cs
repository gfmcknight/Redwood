using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Reflection;
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

    internal class AssignDirectMemberInstruction : Instruction
    {
        private int slot;
        private int valueLocation;

        public AssignDirectMemberInstruction(int slot, int valueLocation)
        {
            this.slot = slot;
            this.valueLocation = valueLocation;
        }

        public int Execute(Frame frame)
        {
            (frame.result as RedwoodObject).slots[slot] = frame.stack[valueLocation];
            return 1;
        }
    }

    internal class AssignExternalFieldInstruction : Instruction
    {
        private FieldInfo info;
        private int valueLocation;

        public AssignExternalFieldInstruction(FieldInfo info, int valueLocation)
        {
            this.info = info;
            this.valueLocation = valueLocation;
        }

        public int Execute(Frame frame)
        {
            info.SetValue(frame.result, frame.stack[valueLocation]);
            return 1;
        }
    }

    internal class AssignExternalPropertyInstruction : Instruction
    {
        private PropertyInfo info;
        private int valueLocation;

        public AssignExternalPropertyInstruction(PropertyInfo info, int valueLocation)
        {
            this.info = info;
            this.valueLocation = valueLocation;
        }

        public int Execute(Frame frame)
        {
            info.SetValue(frame.result, frame.stack[valueLocation]);
            return 1;
        }
    }


    internal class AssignConstructorLambdaInstruction : Instruction
    {
        RedwoodType type;
        internal AssignConstructorLambdaInstruction(RedwoodType type)
        {
            this.type = type;
        }

        public int Execute(Frame frame)
        {
            type.Constructor = frame.result as Lambda;
            return 1;
        }
    }
}

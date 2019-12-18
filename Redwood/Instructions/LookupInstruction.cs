using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Instructions
{
    internal class LookupLocalInstruction : Instruction
    {
        private int index;
        public LookupLocalInstruction(int index)
        {
            this.index = index;
        }
        public int Execute(Frame frame)
        {
            frame.result = frame.stack[index];
            return 1;
        }
    }

    internal class LookupClosureInstruction : Instruction
    {
        private int closureId;
        private int index;

        public LookupClosureInstruction(int closureId, int index)
        {
            this.closureId = closureId;
            this.index = index;
        }

        public int Execute(Frame frame)
        {
            frame.result = frame.closures[closureId].data[index];
            return 1;
        }
    }

    internal class LookupGlobalInstruction : Instruction
    {
        private string name;

        public LookupGlobalInstruction(string name)
        {
            this.name = name;
        }

        public int Execute(Frame frame)
        {
            frame.result = frame.global.LookupVariable(name);
            return 1;
        }
    }

    class LookupDirectMemberInstruction : Instruction
    {
        // TODO: this will work with inheritance?
        private int slot;

        public LookupDirectMemberInstruction(int slot)
        {
            this.slot = slot;
        }

        public int Execute(Frame frame)
        {
            frame.result = (frame.result as RedwoodObject).slots[slot];
            return 1;
        }
    }

    internal class LookupExternalMemberInstruction : Instruction
    {
        private string member;
        private RedwoodType knownType;

        public LookupExternalMemberInstruction(string member, RedwoodType knownType)
        {
            this.member = member;
            this.knownType = knownType;
        }

        public int Execute(Frame frame)
        {
            if (frame.result is RedwoodObject)
            {
                frame.result = (frame.result as RedwoodObject)[member];
            }
            else
            {
                if (MemberResolver.TryResolve(frame.result, knownType, member, out object result))
                {
                    frame.result = result;
                }
                else
                {
                    // TODO: Throw
                }
            }
            return 1;
        }
    }

    internal class LookupExternalMemberLambdaInstruction : Instruction
    {
        private string member;
        private RedwoodType knownType;

        public LookupExternalMemberLambdaInstruction(string member, RedwoodType knownType)
        {
            this.member = member;
            this.knownType = knownType;
        }

        public int Execute(Frame frame)
        {
            if (frame.result is RedwoodObject)
            {
                frame.result = (frame.result as RedwoodObject)[member];
            }
            else
            {
                if (MemberResolver.TryResolveLambda(frame.result, knownType, member, out Lambda result))
                {
                    frame.result = result;
                }
                else
                {
                    // TODO: Throw
                }
            }
            return 1;
        }
    }
}

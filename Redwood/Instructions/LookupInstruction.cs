using Redwood.Ast;
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
                    throw new NotImplementedException();
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
                    throw new NotImplementedException();
                }
            }
            return 1;
        }
    }

    internal class LookupExternalMemberBinaryOperationInstruction : Instruction
    {
        private BinaryOperator op;
        private int leftIndex;
        private int rightIndex;
        private RedwoodType leftKnownType;
        private RedwoodType rightKnownType;

        public LookupExternalMemberBinaryOperationInstruction(
            BinaryOperator op,
            int leftIndex,
            int rightIndex,
            RedwoodType leftKnownType,
            RedwoodType rightKnownType)
        {
            this.op = op;
            this.leftIndex = leftIndex;
            this.rightIndex = rightIndex;
            this.leftKnownType = leftKnownType;
            this.rightKnownType = rightKnownType;
        }
        public int Execute(Frame frame)
        {
            Lambda lambda;
            if (!MemberResolver.TryResolveOperator(
                frame.stack[leftIndex],
                frame.stack[rightIndex],
                leftKnownType,
                rightKnownType,
                op,
                out lambda))
            {
                throw new NotImplementedException();
            }
            frame.result = lambda;
            return 1;
        }
    }

    internal class LookupLambdaGroupOverloadInstruction : Instruction
    {
        private int index;

        public LookupLambdaGroupOverloadInstruction(int index)
        {
            this.index = index;
        }

        public int Execute(Frame frame)
        {
            frame.result = (frame.result as LambdaGroup).lambdas[index];
            return 1;
        }
    }
}

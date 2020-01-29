using System;
using System.Collections.Generic;
using System.Text;
using Redwood.Runtime;

namespace Redwood.Instructions
{
    internal class InternalCallInstruction : Instruction
    {
        private int[] argLocations;

        public InternalCallInstruction(int[] argLocations)
        {
            this.argLocations = argLocations;
        }

        public int Execute(Frame frame)
        {
            Frame newFrame = new Frame(frame.global, frame.result as InternalLambda);
            for (int i = 0; i < argLocations.Length; i++)
            {
                newFrame.stack[i] = frame.stack[argLocations[i]];
            }
            frame.result = newFrame;
            frame.shouldCall = true;
            return 1;
        }
    }

    internal class InPlaceCallInstruction : Instruction
    {
        private int[] argLocations;

        public InPlaceCallInstruction(int[] argLocations)
        {
            this.argLocations = argLocations;
        }

        public int Execute(Frame frame)
        {
            InPlaceLambda lambda = frame.result as InPlaceLambda;
            lambda.RunInPlace(frame, argLocations);
            return 1;
        }
    }

    internal class ExternalCallInstruction : Instruction
    {
        private int[] argLocations;

        public ExternalCallInstruction(int[] argLocations)
        {
            this.argLocations = argLocations;
        }
        public int Execute(Frame frame)
        {
            Lambda lambda = frame.result as Lambda;
            object[] args = new object[argLocations.Length];
            for (int i = 0; i < argLocations.Length; i++)
            {
                args[i] = frame.stack[argLocations[i]];
            }
            frame.result = lambda.Run(args);
            return 1;
        }
    }

    internal class TryCallInstruction : Instruction
    {
        private string functionName;
        private int[] argLocations;
        private RedwoodType calleeTypeHint;
        private RedwoodType[] argTypesHint;

        public TryCallInstruction(RedwoodType[] argTypesHint, int[] argLocations)
        {
            this.argLocations = argLocations;
            this.argTypesHint = argTypesHint;
        }

        public TryCallInstruction(
            string functionName,
            RedwoodType calleeTypeHint,
            RedwoodType[] argTypesHint,
            int[] argLocations)
        {
            this.functionName = functionName;
            this.calleeTypeHint = calleeTypeHint;
            this.argTypesHint = argTypesHint;
            this.argLocations = argLocations;
        }

        public int Execute(Frame frame)
        {
            Lambda lambda;
            if (functionName == null)
            {
                if (!RuntimeUtil.TryConvertToLambda(frame.result, out lambda))
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                if (!MemberResolver.TryResolveLambda(
                    frame.result,
                    calleeTypeHint,
                    functionName,
                    out lambda))
                {
                    throw new NotImplementedException();
                }
            }

            object[] args = new object[argLocations.Length];
            for (int i = 0; i < argLocations.Length; i++)
            {
                args[i] = frame.stack[argLocations[i]];
            }
            if (lambda is LambdaGroup group)
            {
                frame.result = group.RunWithExpectedTypes(argTypesHint, args);
            }
            else
            {
                frame.result = lambda.Run(args);
            }
            return 1;
        }
    }
}

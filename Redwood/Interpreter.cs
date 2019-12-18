using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood
{
    internal static class Interpreter
    {
        internal static object Run(InternalLambda lambda)
        {
            return Run(lambda, lambda.context);
        }

        internal static object Run(InternalLambda lambda, object[] args)
        {
            return Run(lambda, lambda.context, args);
        }

        internal static object Run(InternalLambda lambda, GlobalContext context)
        {
            return Run(lambda, context, new object[0]);
        }

        internal static object Run(InternalLambda lambda, GlobalContext context, object[] args)
        {
            Stack<Frame> frames = new Stack<Frame>();
            Frame currentFrame = new Frame(context, lambda);
            frames.Push(currentFrame);

            // TODO: Are the args guaranteed to the be the
            // first variables on the stack?
            for (int i = 0; i < args.Length; i++)
            {
                currentFrame.stack[i] = args[i];
            }

            while (frames.Count > 0)
            {
                // This returns the result of an InternalCallInstruction
                frames.Peek().result = currentFrame.result;
                currentFrame = frames.Pop();
                // If returning from a function, we should continue from
                // where we last ran 
                int i = currentFrame.returnAddress;
                while (!currentFrame.shouldExit)
                {
                    i += currentFrame.instructions[i].Execute(currentFrame);
                    if (currentFrame.shouldCall)
                    {
                        // Assume that the instruction correctly outputs a
                        // frame into the result; we will overwrite it on
                        // return
                        frames.Push(currentFrame.result as Frame);
                        currentFrame.returnAddress = i;
                        currentFrame.shouldCall = false;
                    }
                }
            }

            return lambda.ReturnType == RedwoodType.Void ? null : currentFrame.result;
        }
    }
}

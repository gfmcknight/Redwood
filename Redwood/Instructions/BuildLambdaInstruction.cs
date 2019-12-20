using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Instructions
{
    internal class BuildLambdaInstruction : Instruction
    {
        private InternalLambdaDescription description;

        internal BuildLambdaInstruction(InternalLambdaDescription description)
        {
            this.description = description;
        }

        public int Execute(Frame frame)
        {
            frame.result = new InternalLambda
            {
                closures = frame.closures, // TODO: Add one closure here or when we run?
                context = frame.global,
                description = description
            };

            return 1;
        }
    }
}

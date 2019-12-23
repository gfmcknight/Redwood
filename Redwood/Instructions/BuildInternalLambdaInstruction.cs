using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Instructions
{
    internal class BuildInternalLambdaInstruction : Instruction
    {
        private InternalLambdaDescription description;

        internal BuildInternalLambdaInstruction(InternalLambdaDescription description)
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

    internal class BuildExternalLambdaInstruction : Instruction
    {
        private MethodGroup group;

        internal BuildExternalLambdaInstruction(MethodGroup group)
        {
            this.group = group;
        }

        public int Execute(Frame frame)
        {
            frame.result = RuntimeUtil.CanonicalizeLambdas(new LambdaGroup(frame.result, group.infos));
            return 1;
        }
    }
}

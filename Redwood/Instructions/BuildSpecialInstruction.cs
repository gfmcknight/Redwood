﻿using Redwood.Runtime;
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
                closures = frame.closures,
                context = frame.global,
                description = description
            };

            return 1;
        }
    }

    internal class BuildInternalLambdasInstruction : Instruction
    {
        private InternalLambdaDescription[] descriptions;

        internal BuildInternalLambdasInstruction(InternalLambdaDescription[] descriptions)
        {
            this.descriptions = descriptions;
        }

        public int Execute(Frame frame)
        {
            InternalLambda[] lambdas = new InternalLambda[descriptions.Length];
            for (int i = 0; i < lambdas.Length; i++)
            {
                lambdas[i] = new InternalLambda
                {
                    closures = frame.closures,
                    context = frame.global,
                    description = descriptions[i]
                };
            }

            frame.result = RuntimeUtil.CanonicalizeLambdas(lambdas);
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

    internal class BuildRedwoodObjectFromClosure : Instruction
    {
        private RedwoodType type;

        internal BuildRedwoodObjectFromClosure(RedwoodType type)
        {
            this.type = type;
        }

        public int Execute(Frame frame)
        {
            RedwoodObject @object = new RedwoodObject();
            @object.Type = type;
            @object.slots = frame.closures[frame.closures.Length - 1].data;
            frame.result = @object;
            return 1;
        }
    }
}

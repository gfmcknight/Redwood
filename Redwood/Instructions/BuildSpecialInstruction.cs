using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Instructions
{
    internal class BuildInternalLambdaInstruction : Instruction
    {
        private InternalLambdaDescription description;

        public BuildInternalLambdaInstruction(InternalLambdaDescription description)
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

        public BuildInternalLambdasInstruction(InternalLambdaDescription[] descriptions)
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

        public BuildExternalLambdaInstruction(MethodGroup group)
        {
            this.group = group;
        }

        public int Execute(Frame frame)
        {
            frame.result = RuntimeUtil.CanonicalizeLambdas(new LambdaGroup(frame.result, group.infos));
            return 1;
        }
    }

    internal class BuildRedwoodObjectFromClosureInstruction : Instruction
    {
        private RedwoodType type;

        public BuildRedwoodObjectFromClosureInstruction(RedwoodType type)
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

    internal class SetStaticOverloadInstruction : Instruction
    {
        private RedwoodType type;
        private int index;

        public SetStaticOverloadInstruction(RedwoodType type, int index)
        {
            this.type = type;
            this.index = index;
        }

        public int Execute(Frame frame)
        {
            type.staticLambdas[index] = frame.result as Lambda;
            return 1;
        }
    }

    internal class BuildLambdaGroupFromLambdasInstruction : Instruction
    {
        private int[] lambdaLocations;

        public BuildLambdaGroupFromLambdasInstruction(int[] lambdaLocations)
        {
            this.lambdaLocations = lambdaLocations;
        }

        public int Execute(Frame frame)
        {
            Lambda[] lambdas = new Lambda[lambdaLocations.Length];
            for (int i = 0; i < lambdaLocations.Length; i++)
            {
                lambdas[i] = frame.stack[lambdaLocations[i]] as Lambda;
            }
            frame.result = new LambdaGroup(lambdas);
            return 1;
        }
    }

    internal class BuildArrayInstruction : Instruction
    {
        private int[] locations;
        Type type;

        public BuildArrayInstruction(int[] location, Type type)
        {
            this.locations = location;
            this.type = type;
        }

        public int Execute(Frame frame)
        {
            Array array = Array.CreateInstance(type, locations.Length);
            for (int i = 0; i < locations.Length; i++)
            {
                array.SetValue(frame.stack[i], i);
            }
            frame.result = array;
            return 1;
        }
    }
}

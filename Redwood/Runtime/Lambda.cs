using Redwood.Instructions;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Redwood.Runtime
{
    public interface Lambda
    {
        IEnumerable<RedwoodType> ExpectedArgs { get; }
        RedwoodType ReturnType { get; }

        object Run(params object[] args);
    }

    internal class InternalLambdaDescription
    {
        internal int stackSize;
        internal int closureSize;
        internal Instruction[] instructions;
        internal RedwoodType[] argTypes;
        internal RedwoodType returnType;

        // If the InternalLambdaDescription describes a
        // lambda belonging to an object, it must appear
        // at a slot
        internal int ownerSlot;
    }

    internal class InternalLambda : Lambda
    {
        internal GlobalContext context;
        internal Closure[] closures;
        internal InternalLambdaDescription description;

        // TODO: Make these immutable
        public IEnumerable<RedwoodType> ExpectedArgs { get => description.argTypes; }
        public RedwoodType ReturnType { get => description.returnType; }

        public object Run(params object[] args)
        {
            IEnumerator<RedwoodType> argsEnumerator = ExpectedArgs.GetEnumerator();
            for (int i = 0; i < args.Length; i++)
            {
                if (!argsEnumerator.MoveNext())
                {
                    throw new ArgumentException("Too few arguments");
                }
                if (argsEnumerator.Current != null &&
                    !argsEnumerator.Current.IsAssignableFrom(args[i]))
                {
                    throw new ArgumentException("Invalid argument type " + args[i].ToString());
                }
            }
            return Interpreter.Run(this, args);
        }
    }

    internal class ExternalLambda : Lambda
    {
        internal object boundTarget;
        internal MethodBase info;

        internal ExternalLambda(RedwoodType type, ConstructorInfo info)
        {
            Init(null, info);
            ReturnType = type;
        }

        internal ExternalLambda(object target, MethodInfo info)
        {
            Init(target, info);
            ReturnType = RedwoodType.GetForCSharpType(info.ReturnType);
        }
        
        private void Init(object target, MethodBase info)
        {
            boundTarget = target;
            this.info = info;
            ParameterInfo[] parameters = info.GetParameters();
            RedwoodType[] expectedArgs = new RedwoodType[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                expectedArgs[i] = RedwoodType.GetForCSharpType(parameters[i].ParameterType);
            }
            ExpectedArgs = expectedArgs;
        }

        public IEnumerable<RedwoodType> ExpectedArgs { get; private set; }
        public RedwoodType ReturnType { get; private set; }

        public object Run(params object[] args)
        {
            IEnumerator<RedwoodType> argsEnumerator = ExpectedArgs.GetEnumerator();
            for (int i = 0; i < args.Length; i++)
            {
                if (!argsEnumerator.MoveNext())
                {
                    throw new ArgumentException("Too few arguments");
                }
                if (!argsEnumerator.Current.IsAssignableFrom(args[i]))
                {
                    throw new ArgumentException("Invalid argument type " + args[i].ToString());
                }
            }

            // TODO: should there get a different lambda type for
            // constructor invocation?
            if (info.IsConstructor)
            {
                return ((ConstructorInfo)info).Invoke(args);
            }

            return info.Invoke(boundTarget, args);
        }
    }

    internal delegate object InPlaceLambdaExecutor(object[] stack, int[] argLocations);

    internal class InPlaceLambda : Lambda
    {
        private RedwoodType[] argumentTypes;
        private InPlaceLambdaExecutor executor;

        public IEnumerable<RedwoodType> ExpectedArgs { get { return argumentTypes; } }
        public RedwoodType ReturnType { get; private set; }

        internal InPlaceLambda(
            RedwoodType[] argumentTypes,
            RedwoodType returnType,
            InPlaceLambdaExecutor executor)
        {
            this.argumentTypes = argumentTypes;
            ReturnType = returnType;
            this.executor = executor;
        }

        public object Run(params object[] args)
        {
            if (args.Length < argumentTypes.Length)
            {
                throw new ArgumentException("Too few arguments");
            }
            // TODO: Check incoming argument types
            int[] argLocations = new int[argumentTypes.Length];
            for (int i = 0; i < argLocations.Length; i++)
            {
                argLocations[i] = i;

                if (!argumentTypes[i].IsAssignableFrom(args[i]))
                {
                    throw new ArgumentException("Invalid argument type " + args[i].ToString());
                }
            }

            return executor(args, argLocations);
        }

        internal void RunInPlace(Frame frame, int[] argLocations)
        {
            // Assume argLocations matches argumentTypes
            frame.result = executor(frame.stack, argLocations);
        }
    }

    internal class LambdaGroup : Lambda
    {
        internal Lambda[] lambdas;
        internal LambdaGroup(Lambda[] lambdas)
        {
            this.lambdas = lambdas;
        }

        internal LambdaGroup(object target, MethodInfo[] infos)
        {
            lambdas = new Lambda[infos.Length];
            for (int i = 0; i < infos.Length; i++)
            {
                if (infos[i].IsStatic)
                {
                    lambdas[i] = new ExternalLambda(null, infos[i]);
                }
                else
                {
                    lambdas[i] = new ExternalLambda(target, infos[i]);
                }
            }
        }

        // TODO: Should either of these have a serious value?
        public IEnumerable<RedwoodType> ExpectedArgs => null;
        public RedwoodType ReturnType => null;

        public object Run(params object[] args)
        {
            RedwoodType[] types = RuntimeUtil.GetTypesFromArgs(args);
            return RuntimeUtil.SelectSingleOverload(types, this).Run(args);
        }

        internal object RunWithExpectedTypes(RedwoodType[] knownArgTypes, object[] args)
        {
            RedwoodType[] types = RuntimeUtil.GetTypesFromArgs(args);
            for (int i = 0; i < knownArgTypes.Length; i++)
            {
                if (knownArgTypes[i] != null)
                {
                    types[i] = knownArgTypes[i];
                }
            }
            return RuntimeUtil.SelectSingleOverload(types, this).Run(args);
        }
    }
}

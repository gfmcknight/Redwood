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
                if (!argsEnumerator.Current.IsAssignableFrom(args[i]))
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
        internal MethodInfo info;

        internal ExternalLambda(object target, MethodInfo info)
        {
            boundTarget = target;
            this.info = info;
        }

        public IEnumerable<RedwoodType> ExpectedArgs => throw new NotImplementedException();
        public RedwoodType ReturnType => throw new NotImplementedException();

        public object Run(params object[] args)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
}

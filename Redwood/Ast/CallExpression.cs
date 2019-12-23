using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Redwood.Instructions;
using Redwood.Runtime;

namespace Redwood.Ast
{
    public class CallExpression : Expression
    {
        public Expression Callee { get; set; }
        public NameExpression FunctionName { get; set; }
        public Expression[] Arguments { get; set; }

        internal List<Variable> ArgumentVariables { get; set; }

        internal override void Bind(Binder binder)
        {
            // We have a series of temporary variables, so we will need
            // to discard them after the call is made
            binder.Bookmark();
            // TODO: definitions in arguments? This shouldn't occur..
            foreach (Expression argument in Arguments)
            {
                argument.Bind(binder);
            }
            foreach (Variable argumentVariable in ArgumentVariables)
            {
                binder.BindVariable(argumentVariable);
            }
            binder.Checkout();
        }

        internal override IEnumerable<Instruction> Compile()
        {
            // First, create all the temporary variables we need
            // in order to compute
            bool fullyResolvedTypes = true;
            List<Instruction> instructions = new List<Instruction>();
            RedwoodType[] argumentTypes = new RedwoodType[Arguments.Length];
            int[] argumentLocations = new int[Arguments.Length];
            for (int i = 0; i < Arguments.Length; i++)
            { 
                argumentTypes[i] = Arguments[i].GetKnownType();
                fullyResolvedTypes &= argumentTypes[i] != null;
                argumentLocations[i] = ArgumentVariables[i].Location;
                instructions.AddRange(Arguments[i].Compile());
                instructions.Add(Compiler.CompileVariableAssign(ArgumentVariables[i]));
            }

            if (Callee == null)
            {
                instructions.AddRange(FunctionName.Compile());
                RedwoodType knownType = FunctionName.GetKnownType();
                if (knownType == null)
                {
                    instructions.Add(new TryCallInstruction(argumentTypes, argumentLocations));
                }
                else if (RedwoodType
                            .GetForCSharpType(typeof(InternalLambda))
                            .IsAssignableFrom(knownType))
                {
                    instructions.Add(new InternalCallInstruction(argumentLocations));
                }
                else if (RedwoodType
                            .GetForCSharpType(typeof(Lambda))
                            .IsAssignableFrom(knownType))
                {
                    instructions.Add(new ExternalCallInstruction(argumentLocations));
                }
            }
            else
            {
                instructions.AddRange(Callee.Compile());
                RedwoodType calleeType = Callee.GetKnownType();
                if (calleeType == null)
                {
                    // Try to resolve on the fly if we can't figure it out
                    instructions.Add(new LookupExternalMemberLambdaInstruction(FunctionName.Name, calleeType));
                    instructions.Add(new TryCallInstruction(argumentTypes, argumentLocations));
                }
                else
                {
                    if (calleeType.CSharpType == null)
                    {
                        instructions.Add(
                            new LookupDirectMemberInstruction(
                                calleeType.GetSlotNumberForOverload(FunctionName.Name, argumentTypes)));
                        instructions.Add(new InternalCallInstruction(argumentLocations));
                    }
                    else
                    {
                        // TODO: Resolve based on the known types of arguments?
                        MethodInfo[] infos;
                        MemberResolver.TryResolveMethod(null, calleeType, FunctionName.Name, out infos);
                        MethodGroup group = new MethodGroup(infos);
                        group.SelectOverloads(argumentTypes);

                        if (group.infos.Length == 0)
                        {
                            throw new NotImplementedException();
                        }

                        instructions.Add(new BuildExternalLambdaInstruction(group));
                        instructions.Add(new ExternalCallInstruction(argumentLocations));
                    }
                }
            }

            
            return instructions;
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            ArgumentVariables = new List<Variable>();
            for (int i = 0; i < Arguments.Length; i++)
            {
                ArgumentVariables.Add(new Variable
                {
                    Temporary = true
                });
            }

            List<NameExpression> freeVariables = new List<NameExpression>();
            if (Callee == null)
            {
                freeVariables.Add(FunctionName);
            }
            else
            {
                freeVariables.AddRange(Callee.Walk());
            }

            foreach (Expression argument in Arguments)
            {
                freeVariables.AddRange(argument.Walk());
            }
            return freeVariables;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
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
        internal bool FullyResolved { get; set; }
        internal RedwoodType LambdaType { get; set; }
        internal MethodGroup ExternalMethodGroup { get; set; }

        internal override void Bind(Binder binder)
        {
            // We have a series of temporary variables, so we will need
            // to discard them after the call is made
            binder.Bookmark();
            
            // We have the process
            // Eval arg i, assign variable i, eval arg i+1, assign variable i+1
            // so if we, for instance, bind every argument, then bind every temp
            // variable, we are sure to clobber our own arguments
            for (int i = 0; i < Arguments.Length; i++)
            {
                Arguments[i].Bind(binder);
                binder.BindVariable(ArgumentVariables[i]);
            }
            
            binder.Checkout();


            bool fullyResolvedTypes;
            if (Callee == null)
            {
                fullyResolvedTypes = FunctionName.GetKnownType() != null;
            }
            else
            {
                fullyResolvedTypes = Callee.GetKnownType() != null;
            }

            RedwoodType[] argumentTypes = new RedwoodType[Arguments.Length];
            for (int i = 0; i < Arguments.Length; i++)
            {
                argumentTypes[i] = Arguments[i].GetKnownType();
                // Not really sure whether we care about this, but it might be useful
                // for compiling variable assignments
                ArgumentVariables[i].KnownType = argumentTypes[i];
                fullyResolvedTypes &= argumentTypes[i] != null;
            }
            
            // If we have fully resolved types (ie. all argument types are known
            // and so is the callee type) we definitely know the return type
            FullyResolved = fullyResolvedTypes;
            // We have two options for determining an exact type, either:
            // 1) The method is fully resolved. One lambda type will come out of
            //    the many,
            // or
            // 2) the method is not fully resolved, but the provided arguments
            //    narrow the methods down a single option.
            if (Callee == null)
            {
                LambdaType = FunctionName.GetKnownType();
            }
            else if (Callee.GetKnownType() == null)
            {
                LambdaType = null;
            }
            else if (Callee.GetKnownType().CSharpType == null)
            {
                // TODO
                throw new NotImplementedException();
            }
            else
            {
                MethodInfo[] infos;
                bool methodExists = MemberResolver.TryResolveMethod(
                    null,
                    Callee.GetKnownType(),
                    FunctionName.Name,
                    false,
                    out infos);
                
                if (!methodExists)
                {
                    // TODO: Can an object have a lambda field?
                    throw new NotImplementedException();
                }
                ExternalMethodGroup = new MethodGroup(infos);
                ExternalMethodGroup.SelectOverloads(argumentTypes);

                if (ExternalMethodGroup.infos.Length == 0)
                {
                    throw new NotImplementedException();
                }
                else if (ExternalMethodGroup.infos.Length == 1)
                {
                    RedwoodType returnType =
                        RedwoodType.GetForCSharpType(ExternalMethodGroup.infos[0].ReturnType);
                   
                    RedwoodType[] paramTypes = ExternalMethodGroup.infos[0].GetParameters()
                        .Select(param => RedwoodType.GetForCSharpType(param.ParameterType))
                        .ToArray();

                    LambdaType = RedwoodType.GetForLambdaArgsTypes(
                        typeof(ExternalLambda),
                        returnType,
                        paramTypes);
                }
            }
        }

        internal override IEnumerable<Instruction> Compile()
        {
            // First, create all the temporary variables we need
            // in order to compute
            List<Instruction> instructions = new List<Instruction>();
            RedwoodType[] argumentTypes = new RedwoodType[Arguments.Length];
            int[] argumentLocations = new int[Arguments.Length];
            for (int i = 0; i < Arguments.Length; i++)
            { 
                argumentTypes[i] = Arguments[i].GetKnownType();
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
                else if (knownType.CSharpType == typeof(InternalLambda))
                {
                    instructions.Add(new InternalCallInstruction(argumentLocations));
                }
                else if (knownType.CSharpType == typeof(ExternalLambda))
                {
                    instructions.Add(new ExternalCallInstruction(argumentLocations));
                }
                else if (knownType.CSharpType == typeof(RedwoodType))
                {
                    instructions.Add(new LookupExternalMemberLambdaInstruction("Constructor", knownType));
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
                        instructions.Add(new BuildExternalLambdaInstruction(ExternalMethodGroup));
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

        public override RedwoodType GetKnownType()
        {
            if (LambdaType == null)
            {
                return null;
            }

            RedwoodType[] signature = LambdaType.GenericArguments;
            // Don't know the type of the lambda's arguments/return?
            if (signature == null || signature.Length == 0)
            {
                throw new NotImplementedException();
            }

            // The lambda type is Lambda<ParamTypes..., ReturnType>
            return signature[signature.Length - 1];
        }

        internal override IEnumerable<Instruction> CompileLVal()
        {
            throw new NotImplementedException();
        }
    }
}

using Redwood.Instructions;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Redwood.Ast
{
    class InterfaceDefinition : Definition
    {
        // We're going to have these function definitions, but we will
        // guarantee that they are stubs
        public FunctionDefinition[] Methods { get; set; }

        internal RedwoodType Type { get; set; }

        internal Variable This { get; set; }
        internal List<Variable> Variables { get; set; }
        // The arguments to be used in the creation of the interface object
        internal List<Variable> ArgumentVariables { get; set; }
        // The members that need to get assigned in the constructor -- these come
        // from the argument variables
        internal List<Variable> SuppliedVariables { get; set; }
        internal List<OverloadGroup> Overloads { get; set; }

        internal override IEnumerable<NameExpression> Walk()
        {
            Type = RedwoodType.Make(this);
            base.Walk();
            DeclaredVariable.KnownType = RedwoodType.GetForCSharpType(typeof(RedwoodType));
            DeclaredVariable.DefinedConstant = true;
            DeclaredVariable.ConstantValue = Type;

            List<NameExpression> freeVars = new List<NameExpression>();
            List<Variable> declaredVars = new List<Variable>();
            
            // The set of variables which must be supplied when creating the interface
            List<Variable> varsSupplied = new List<Variable>();

            This = new Variable
            {
                Name = "this",
                KnownType = null, // Dynamic since the reference isn't necessarily our own type
            };
            declaredVars.Add(This);
            varsSupplied.Add(This);

            foreach (FunctionDefinition method in Methods)
            {
                // Make sure that the method is a stub
                if (method.Body != null)
                {
                    throw new NotImplementedException();
                }

                freeVars.AddRange(method.Walk());
                // As in the ClassDefinition, these need to be closured variables
                method.DeclaredVariable.Closured = true;
                varsSupplied.Add(method.DeclaredVariable);
            }

            // For every raw function and the this variable,
            // we're going to need to take is as an argument
            // for building the interface. Each of these should
            // live on the stack as they are variables.
            ArgumentVariables = varsSupplied
                .Select(variable =>
                    new Variable
                    {
                        Name = variable.Name
                    }
                )
                .ToList();
            SuppliedVariables = varsSupplied;

            Overloads = Compiler.GenerateOverloads(Methods.ToList());
            declaredVars.AddRange(Overloads.Select(o => o.variable));

            foreach (Variable variable in declaredVars)
            {
                variable.Closured = true;
            }

            Variables = declaredVars;

            // There is no need to match against the fields of the class as
            // all methods are stubs, and no code is closured against the
            // interface itself.
            return freeVars;
        }

        internal override void Bind(Binder binder)
        {
            base.Bind(binder);

            // Interfaces can't have static members
            Type.staticLambdas = new Lambda[0];
            Type.staticSlotTypes = new RedwoodType[0];
            Type.staticSlotMap = new Dictionary<string, int>();

            binder.EnterFullScope();

            binder.BindVariable(This);

            foreach (Variable constructorArgument in ArgumentVariables)
            {
                binder.BindVariable(constructorArgument);
            }

            foreach (FunctionDefinition method in Methods)
            {
                method.Bind(binder);
            }

            foreach (OverloadGroup overload in Overloads)
            {
                overload.DoBind(binder);

                RedwoodType[][] signatures = overload
                    .definitions
                    .Select(def =>
                        def.Parameters.Select(param => param.Type.GetIndicatedType()).ToArray()
                    )
                    .ToArray();

                int[] slots = overload
                    .definitions
                    .Select(def => def.DeclaredVariable.Location)
                    .ToArray();

                Type.overloadsMap[overload.variable.Location] =
                    new Tuple<RedwoodType[][], int[]>(signatures, slots);
            }

            // Since our class is just a closure
            Type.numSlots = binder.GetClosureSize();

            Type.slotMap = new Dictionary<string, int>();
            Type.slotTypes = new RedwoodType[Type.numSlots];

            // Ensure that even though the overloads are hidden, they
            // are still represented in the slot map
            foreach (FunctionDefinition method in Methods)
            {
                Type.slotTypes[method.DeclaredVariable.Location] = method.DeclaredVariable.KnownType;
            }

            for (int i = 0; i < Variables.Count; i++)
            {
                Variable var = Variables[i];
                int slot = var.Location;
                Type.slotTypes[slot] = var.KnownType;
                Type.slotMap[var.Name] = slot;
            }

            binder.LeaveFullScope();
        }

        internal override IEnumerable<Instruction> Compile()
        {
            List<Instruction> constructorInstructions = new List<Instruction>();

            // For everything that isn't an overload group, we're going to
            // trust it as an argument and put it into our class closure.
            for (int i = 0; i < ArgumentVariables.Count; i++)
            {
                constructorInstructions.Add(Compiler.CompileVariableLookup(ArgumentVariables[i]));
                constructorInstructions.Add(Compiler.CompileVariableAssign(SuppliedVariables[i]));
            }

            foreach (OverloadGroup overload in Overloads)
            {
                if (overload.IsSingleMethod())
                {
                    continue;
                }

                int[] lambdaLocations = ArgumentVariables
                    .Where(variable => variable.Name == overload.name)
                    .Select(variable => variable.Location)
                    .ToArray();
                constructorInstructions.Add(new BuildLambdaGroupFromLambdasInstruction(lambdaLocations));
                constructorInstructions.Add(Compiler.CompileVariableAssign(overload.variable));
            }

            constructorInstructions.Add(new BuildRedwoodObjectFromClosureInstruction(Type));
            constructorInstructions.Add(new ReturnInstruction());

            InternalLambdaDescription constructorLambda = new InternalLambdaDescription
            {
                argTypes = SuppliedVariables
                    .Select(argVar => null as RedwoodType)
                    .ToArray(),
                returnType = Type,
                closureSize = Type.numSlots,
                stackSize = ArgumentVariables.Count,
                instructions = constructorInstructions.ToArray()
            };

            return new Instruction[]
            {
                new BuildInternalLambdaInstruction(constructorLambda),
                new AssignConstructorLambdaInstruction(Type),
                new LoadConstantInstruction(Type),
                Compiler.CompileVariableAssign(DeclaredVariable)
            };
        }
    }
}

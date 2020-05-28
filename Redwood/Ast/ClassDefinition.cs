using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Redwood.Instructions;
using Redwood.Runtime;

namespace Redwood.Ast
{
    public class ClassDefinition : Definition
    {
        // Parameter fields are for default constructors -- where fields
        // and the constructor can be defined all at once.
        public ParameterDefinition[] ParameterFields { get; set; }
        public TypeSyntax[] Interfaces { get; set; }
        public LetDefinition[] InstanceFields { get; set; }
        public FunctionDefinition[] Constructors { get; set; }
        public FunctionDefinition[] Methods { get; set; }
        public FunctionDefinition[] StaticMethods { get; set; }
        internal RedwoodType Type { get; set; }
        internal Variable This { get; set; }
        internal List<Variable> MemberVariables { get; set; }
        internal List<Variable> InterfaceImplicitConversionVars { get; set; }
        internal List<Variable> TempArgumentVariables { get; set; }
        internal List<Instruction> ConstructorBase { get; set; }
        internal int ConstructorStackSize { get; set; }
        internal List<OverloadGroup> Overloads { get; set; }
        internal List<OverloadGroup> StaticOverloads { get; set; }

        internal override void Bind(Binder binder)
        {
            // TODO: Base type binding?
            base.Bind(binder);

            // Bind using local variables when building the global context
            // so that the lambdas can be available immediately when the
            // module is initialized.
            Type.staticLambdas = new Lambda[StaticOverloads.Count];
            Type.staticSlotTypes = new RedwoodType[StaticOverloads.Count];
            Type.staticSlotMap = new Dictionary<string, int>();

            // TODO: Is it okay to make these temporary?
            binder.Bookmark();

            foreach (FunctionDefinition method in StaticMethods)
            {
                method.Bind(binder);
            }

            for (int i = 0; i < StaticOverloads.Count; i++)
            {
                OverloadGroup overload = StaticOverloads[i];
                overload.DoBind(binder);

                Type.staticSlotTypes[i] = overload.variable.KnownType;
                Type.staticSlotMap[overload.name] = i;
            }
            binder.Checkout();

            binder.EnterFullScope();

            binder.Bookmark();
            if (ParameterFields != null)
            {
                foreach (ParameterDefinition parameterField in ParameterFields)
                {
                    parameterField.Bind(binder);
                }
            }
            binder.Checkout();

            // We need to support a variable number of arguments
            // because all constructors are compiled to the same
            // thing
            foreach (Variable argVariable in TempArgumentVariables)
            {
                binder.BindVariable(argVariable);
            }

            foreach (FunctionDefinition constructor in Constructors)
            {
                constructor.Bind(binder);
            }

            binder.BindVariable(This);

            foreach (LetDefinition field in InstanceFields)
            {
                field.Bind(binder);
            }

            foreach (FunctionDefinition method in Methods)
            {
                method.Bind(binder);
            }

            foreach (Variable implicitConversionVariable in InterfaceImplicitConversionVars)
            {
                binder.BindVariable(implicitConversionVariable);
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

            for (int i = 0; i < MemberVariables.Count; i++)
            {
                Variable var = MemberVariables[i];
                int slot = var.Location;
                Type.slotTypes[slot] = var.KnownType;
                Type.slotMap[var.Name] = slot;
            }

            Type.Interfaces = Interfaces
                .Select(typeSyntax => typeSyntax.GetIndicatedType())
                .ToArray();
            Type.implicitConversionMap = new Dictionary<RedwoodType, int>();
            for (int i = 0; i < Interfaces.Length; i++)
            {
                RedwoodType interfaceType = Interfaces[i].GetIndicatedType();
                Type.implicitConversionMap[interfaceType] = InterfaceImplicitConversionVars[i].Location;
            }

            ConstructorStackSize = binder.LeaveFullScope();
        }

        private InternalLambdaDescription CompileInterfaceConversion(RedwoodType type)
        {
            int closureId = This.ClosureID;
            int[] slots = RuntimeUtil.GetSlotMapsToInterface(Type, type);
            List<Instruction> instructions = new List<Instruction>();
            for (int i = 0; i < slots.Length; i++)
            {
                // Get the member on our class
                instructions.Add(new LookupClosureInstruction(closureId, slots[i]));
                // Assign it as an argument for the closure
                instructions.Add(new AssignLocalInstruction(i));
            }

            instructions.Add(new LoadConstantInstruction(type));
            instructions.Add(new LookupExternalMemberLambdaInstruction("Constructor", type));
            // We already arranged all of the arguments in order
            instructions.Add(
                new InternalCallInstruction(
                    Enumerable
                        .Range(0, slots.Length)
                        .ToArray()
                )
            );
            instructions.Add(new ReturnInstruction());

            return new InternalLambdaDescription
            {
                argTypes = new RedwoodType[0],
                closureSize = 0,
                stackSize = slots.Length,
                returnType = type,
                instructions = instructions.ToArray()
            };
        }

        internal List<Instruction> CompileConstructor(FunctionDefinition constructor)
        {
            List<Instruction> instructions = new List<Instruction>();
            instructions.Add(new BuildRedwoodObjectFromClosureInstruction(Type));
            instructions.Add(Compiler.CompileVariableAssign(This));

            if (ConstructorBase == null)
            {
                ConstructorBase = new List<Instruction>();

                foreach (LetDefinition field in InstanceFields)
                {
                    ConstructorBase.AddRange(field.Compile());
                }

                foreach (FunctionDefinition method in Methods)
                {
                    ConstructorBase.AddRange(method.Compile());
                }

                for (int i = 0; i < Interfaces.Length; i++)
                {
                    ConstructorBase.Add(
                        new BuildInternalLambdaInstruction(
                            CompileInterfaceConversion(Interfaces[i].GetIndicatedType())
                        )
                    );
                    ConstructorBase.Add(
                        Compiler.CompileVariableAssign(InterfaceImplicitConversionVars[i])
                    );
                }

                foreach (OverloadGroup method in Overloads)
                {
                    if (!method.IsSingleMethod())
                    {
                        ConstructorBase.AddRange(method.Compile());
                    }
                }
            }
            instructions.AddRange(ConstructorBase);

            // TODO: should these be mutually exclusive for a class?
            if (constructor == null)
            {
                foreach (ParameterDefinition paramField in ParameterFields)
                {
                    instructions.AddRange(paramField.Compile());
                }
            }
            else
            {
                InternalLambdaDescription constructorDescription = constructor.CompileInner();

                int[] argLocations = new int[constructorDescription.argTypes.Length];
                for (int i = 0; i < argLocations.Length; i++)
                {
                    argLocations[i] = i;
                }

                instructions.Add(new BuildInternalLambdaInstruction(constructorDescription));
                instructions.Add(new InternalCallInstruction(argLocations));
            }

            instructions.Add(Compiler.CompileVariableLookup(This));
            instructions.Add(new ReturnInstruction());
            return instructions;
        }

        internal override IEnumerable<Instruction> Compile()
        {
            List<InternalLambdaDescription> constructorOverloads = new List<InternalLambdaDescription>();
            if (ParameterFields != null)
            {
                constructorOverloads.Add(new InternalLambdaDescription
                {
                    argTypes = ParameterFields.Select(param => param.Type.GetIndicatedType()).ToArray(),
                    returnType = Type,
                    closureSize = Type.numSlots,
                    stackSize = ConstructorStackSize,
                    instructions = CompileConstructor(null).ToArray()
                });
            }

            foreach (FunctionDefinition constructor in Constructors)
            {
                constructorOverloads.Add(new InternalLambdaDescription
                {
                    argTypes = constructor.Parameters.Select(param => param.Type.GetIndicatedType()).ToArray(),
                    returnType = Type,
                    closureSize = Type.numSlots,
                    stackSize = ConstructorStackSize,
                    instructions = CompileConstructor(constructor).ToArray()
                });
            }

            List<Instruction> instructions = new List<Instruction>();

            instructions.AddRange(
                new Instruction[]
                {
                    new BuildInternalLambdasInstruction(constructorOverloads.ToArray()),
                    new AssignConstructorLambdaInstruction(Type),
                    new LoadConstantInstruction(Type),
                    Compiler.CompileVariableAssign(DeclaredVariable)
                }
            );

            for (int i = 0; i < StaticOverloads.Count; i++)
            {
                OverloadGroup overload = StaticOverloads[i];
                instructions.AddRange(overload.Compile());
                instructions.Add(Compiler.CompileVariableLookup(overload.variable));
                instructions.Add(new SetStaticOverloadInstruction(Type, i));
            }
            return instructions;
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            Type = RedwoodType.Make(this);
            base.Walk();
            DeclaredVariable.KnownType = RedwoodType.GetForCSharpType(typeof(RedwoodType));
            DeclaredVariable.DefinedConstant = true;
            DeclaredVariable.ConstantValue = Type;
            List<NameExpression> freeVars = new List<NameExpression>();
            List<Variable> declaredVars = new List<Variable>();
            InterfaceImplicitConversionVars = new List<Variable>();
            int maxConstructorArgs = 0;

            This = new Variable
            {
                Name = "this",
                KnownType = Type
            };
            declaredVars.Add(This);

            if (ParameterFields != null)
            {
                maxConstructorArgs = ParameterFields.Length;
                foreach (ParameterDefinition param in ParameterFields)
                {
                    freeVars.AddRange(param.Walk());
                    declaredVars.Add(param.DeclaredVariable);
                }
            }

            foreach (TypeSyntax interfaceType in Interfaces)
            {
                freeVars.AddRange(interfaceType.Walk());
                // TODO: What if we inherit an implicit, or if we
                // a function that is meant to represent this, or
                // an implicit declared function?
                InterfaceImplicitConversionVars.Add(
                    new Variable
                    {
                        Name = RuntimeUtil.GetNameOfConversionToType(interfaceType.TypeName.Name),
                        Closured = true,
                        DefinedConstant = true
                    }
                );
            }

            foreach (LetDefinition field in InstanceFields)
            {
                freeVars.AddRange(field.Walk());
                declaredVars.Add(field.DeclaredVariable);
            }

            foreach (FunctionDefinition constructor in Constructors)
            {
                freeVars.AddRange(constructor.Walk());
                maxConstructorArgs = Math.Max(maxConstructorArgs, constructor.Parameters.Length);
            }

            TempArgumentVariables = new List<Variable>();
            for (int i = 0; i < maxConstructorArgs; i++)
            {
                TempArgumentVariables.Add(new Variable
                {
                    Temporary = true
                });
            }

            foreach (FunctionDefinition method in Methods)
            {
                freeVars.AddRange(method.Walk());
                // Closure these variables even though they aren't in the
                // object's map so that they can be directly accessed
                method.DeclaredVariable.Closured = true;
            }
            Overloads = Compiler.GenerateOverloads(Methods.ToList());
            declaredVars.AddRange(Overloads.Select(o => o.variable));

            declaredVars.AddRange(InterfaceImplicitConversionVars);
            MemberVariables = declaredVars;
            // Make sure that all of our variables end up in the closure that
            // makes up our RedwoodObject
            foreach (Variable member in declaredVars)
            {
                member.Closured = true;
            }

            // Treat the class as a closure that can be populated and then
            // updated by all methods.
            Compiler.MatchVariables(freeVars, declaredVars);

            // When it comes to static methods, we don't want to match to
            // our own instance variables.
            foreach (FunctionDefinition method in StaticMethods)
            {
                freeVars.AddRange(method.Walk());
            }
            StaticOverloads = Compiler.GenerateOverloads(StaticMethods.ToList());

            return freeVars;
        }
    }
}

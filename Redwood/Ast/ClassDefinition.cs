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
        public LetDefinition[] InstanceFields { get; set; }
        public FunctionDefinition[] Constructors { get; set; }
        public FunctionDefinition[] Methods { get; set; }
        internal RedwoodType Type { get; set; }
        internal List<Variable> MemberVariables { get; set; }
        internal List<Variable> TempArgumentVariables { get; set; }
        internal List<Instruction> ConstructorBase { get; set; }
        internal int ConstructorStackSize { get; set; }
        internal List<OverloadGroup> Overloads { get; set; }

        internal override void Bind(Binder binder)
        {
            // TODO: Base type binding?
            base.Bind(binder);
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

            foreach (LetDefinition field in InstanceFields)
            {
                field.Bind(binder);
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
            for (int i = 0; i < MemberVariables.Count; i++)
            {
                Variable var = MemberVariables[i];
                int slot = var.Location;
                Type.slotTypes[slot] = var.KnownType;
                Type.slotMap[var.Name] = slot;
            }

            ConstructorStackSize = binder.LeaveFullScope();
        }

        internal List<Instruction> CompileConstructor(FunctionDefinition constructor)
        {
            List<Instruction> instructions = new List<Instruction>();

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

            instructions.Add(new BuildRedwoodObjectFromClosure(Type));
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

            return new Instruction[]
            {
                new BuildInternalLambdasInstruction(constructorOverloads.ToArray()),
                new AssignConstructorLambda(Type),
                new LoadConstantInstruction(Type),
                Compiler.CompileVariableAssign(DeclaredVariable)
            };
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
            int maxConstructorArgs = 0;

            if (ParameterFields != null)
            {
                maxConstructorArgs = ParameterFields.Length;
                foreach (ParameterDefinition param in ParameterFields)
                {
                    freeVars.AddRange(param.Walk());
                    declaredVars.Add(param.DeclaredVariable);
                }
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
            return freeVars;
        }
    }
}

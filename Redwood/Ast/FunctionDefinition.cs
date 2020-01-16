﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Redwood.Instructions;
using Redwood.Runtime;

namespace Redwood.Ast
{
    public class FunctionDefinition : Definition
    {
        public BlockStatement Body { get; set; }
        public ParameterDefinition[] Parameters { get; set; }
        public bool ClassMethod { get; set; }
        public bool Static { get; set; }
        public TypeSyntax ReturnType { get; set; }

        internal int StackSize { get; set; }
        internal int ClosureSize { get; set; }

        internal override void Bind(Binder binder)
        {
            DeclaredVariable.KnownType = RedwoodType.GetForLambdaArgsTypes(
                typeof(InternalLambda),
                ReturnType?.GetIndicatedType() ?? RedwoodType.Void,
                Parameters.Select(param => param.Type.GetIndicatedType()).ToArray());
            base.Bind(binder);
            binder.EnterFullScope();
            foreach (ParameterDefinition param in Parameters)
            {
                param.Bind(binder);
            }
            Body.Bind(binder);
            ClosureSize = binder.GetClosureSize();
            StackSize = binder.LeaveFullScope();
        }

        internal override IEnumerable<Instruction> Compile()
        {
            return new Instruction[]
            {
                new BuildInternalLambdaInstruction(CompileInner()),
                Compiler.CompileVariableAssign(DeclaredVariable)
            };

        }

        internal override IEnumerable<NameExpression> Walk()
        {
            List<NameExpression> freeVars = base.Walk().ToList();
            // Having no return type syntax -> Void return type
            if (ReturnType != null)
            {
                freeVars.AddRange(ReturnType.Walk());
            }

            List<Variable> parameterVars = new List<Variable>();
            foreach (ParameterDefinition param in Parameters)
            {
                freeVars.AddRange(param.Walk());
                parameterVars.Add(param.DeclaredVariable);
            }

            freeVars.AddRange(Body.Walk());

            Compiler.MatchVariables(freeVars, parameterVars);
            // Anything that escapes from the function definition
            // will need to be closured because it comes from an actual
            // outer function scope
            foreach (NameExpression ne in freeVars)
            {
                ne.RequiresClosure = true;
            }
            return freeVars;
        }

        internal InternalLambdaDescription CompileInner()
        {
            List<Instruction> bodyInstructions = new List<Instruction>();
            RedwoodType[] paramTypes = new RedwoodType[Parameters.Length];
            for (int i = 0; i < Parameters.Length; i++)
            {
                paramTypes[i] = Parameters[i].DeclaredVariable.KnownType;
                bodyInstructions.AddRange(Parameters[i].Compile());
            }

            bodyInstructions.AddRange(Body.Compile());
            if (ReturnType == null || ReturnType.GetIndicatedType() == RedwoodType.Void)
            {
                bodyInstructions.Add(new ReturnInstruction());
            }

            return new InternalLambdaDescription
            {
                argTypes = paramTypes,
                returnType = ReturnType?.GetIndicatedType() ?? RedwoodType.Void,
                instructions = bodyInstructions.ToArray(),
                stackSize = StackSize,
                closureSize = ClosureSize,
                ownerSlot = DeclaredVariable.Location // TODO: is this right?
            };
        }
    }
}

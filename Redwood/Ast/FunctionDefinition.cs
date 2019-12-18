using System;
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

        internal int stackSize;

        internal override void Bind(Binder binder)
        {
            base.Bind(binder);
            binder.EnterFullScope();
            foreach (ParameterDefinition param in Parameters)
            {
                param.Bind(binder);
            }
            Body.Bind(binder);
            stackSize = binder.LeaveFullScope();
        }

        internal override IEnumerable<Instruction> Compile()
        {
            throw new NotImplementedException();
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            List<NameExpression> freeVars = base.Walk().ToList();
            freeVars.AddRange(ReturnType.Walk());

            List<Variable> parameterVars = new List<Variable>();
            foreach (ParameterDefinition param in Parameters)
            {
                param.Walk();
                parameterVars.Add(param.DeclaredVariable);
            }

            freeVars.AddRange(Body.Walk());
            Compiler.MatchVariables(freeVars, parameterVars);
            return freeVars;
        }

        internal InternalLambdaDescription CompileInner()
        {
            RedwoodType[] paramTypes = new RedwoodType[Parameters.Length];
            for (int i = 0; i < Parameters.Length; i++)
            {
                paramTypes[i] = Parameters[i].Type.GetIndicatedType();
            }

            return new InternalLambdaDescription
            {
                argTypes = paramTypes,
                returnType = ReturnType.GetIndicatedType(), // TODO!
                instructions = Body.Compile().ToArray(),
                stackSize = stackSize,
                ownerSlot = DeclaredVariable.Location // TODO: is this right?
            };
        }
    }
}

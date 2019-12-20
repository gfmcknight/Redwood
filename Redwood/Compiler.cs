using Redwood.Ast;
using Redwood.Instructions;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Redwood
{
    internal class Binder
    {
        private int scopeDepth;
        private List<int> maxStack;
        private List<int> stackPosition;
        private List<int> closurePosition;

        internal Binder()
        {
            scopeDepth = -1;
            maxStack = new List<int>();
            stackPosition = new List<int>();
            closurePosition = new List<int>();
        }

        internal void BindVariable(Variable variable)
        {
            // TODO: Don't bind if we don't have anything to bind to?
            if (scopeDepth < 0)
            {
                return;
            }

            if (variable.Closured)
            {
                variable.ClosureID = closurePosition.Count - 1;
                variable.Location = closurePosition[closurePosition.Count - 1];
                closurePosition[closurePosition.Count - 1]++;
            }
            else
            {
                variable.Location = stackPosition[scopeDepth];
                stackPosition[scopeDepth]++;
                maxStack[scopeDepth] = Math.Max(stackPosition[scopeDepth], maxStack[scopeDepth]);
            }
        }

        internal void EnterFullScope()
        {
            maxStack.Add(0);
            stackPosition.Add(0);
            closurePosition.Add(0);
            scopeDepth += 1;
        }

        internal int LeaveFullScope()
        {
            int stackSize = maxStack[scopeDepth];
            maxStack.RemoveAt(scopeDepth);
            stackPosition.RemoveAt(scopeDepth);
            closurePosition.RemoveAt(scopeDepth);
            scopeDepth -= 1;
            return stackSize;
        }

        internal int GetClosureSize()
        {
            return closurePosition[closurePosition.Count - 1];
        }

        // A way of tracking the fact that some variables will
        // have a lifetime is needed, such as when inside of an
        // if statement and the variables can be reset
        internal void Bookmark()
        {
            maxStack.Add(maxStack[scopeDepth]);
            stackPosition.Add(stackPosition[scopeDepth]);
            scopeDepth += 1;
        }

        internal void Checkout()
        {
            int stackSize = maxStack[scopeDepth];
            maxStack.RemoveAt(scopeDepth);
            stackPosition.RemoveAt(scopeDepth);
            scopeDepth -= 1;
            maxStack[scopeDepth] = Math.Max(stackSize, maxStack[scopeDepth]);
        }
    }

    public static class Compiler
    {
        internal static void MatchVariables(IList<NameExpression> freeVariables,
            IEnumerable<Variable> declaredVariables)
        {
            Dictionary<string, Variable> variablesByName =
                declaredVariables.ToDictionary(variable => variable.Name);

            // Match all variables we found within our own scope
            for (int i = freeVariables.Count - 1; i >= 0; i--)
            {
                string name = freeVariables[i].Name;
                if (variablesByName.ContainsKey(name))
                {
                    if (freeVariables[i].RequiresClosure)
                    {
                        variablesByName[name].Closured = true;
                    }
                    freeVariables[i].Variable = variablesByName[name];
                    freeVariables.RemoveAt(i);
                }
            }
        }

        internal static Instruction CompileVariableLookup(Variable variable)
        {
            if (variable.Closured)
            {
                return new LookupClosureInstruction(variable.ClosureID, variable.Location);
            }
            else if (variable.Global)
            {
                return new LookupGlobalInstruction(variable.Name);
            }
            else
            {
                return new LookupLocalInstruction(variable.Location);
            }
        }

        internal static Instruction CompileVariableAssign(Variable variable)
        {
            if (variable.Closured)
            {
                return new AssignClosureInstruction(variable.ClosureID, variable.Location);
            }
            else if (variable.Global)
            {
                return new AssignGlobalInstruction(variable.Name);
            }
            else
            {
                return new AssignLocalInstruction(variable.Location);
            }
        }

        public static Lambda CompileFunction(FunctionDefinition function)
        {
            // TODO!
            IEnumerable<NameExpression> freeVars = function.Walk();
            if (freeVars.Count() > 0)
            {
                // TODO
                throw new NotImplementedException("Compiling functions with free variables");
            }
            // Binding is needed for the function to know what sort of a stack size
            // is needed by the body, but isn't needed for our purposes here
            function.Bind(new Binder());

            GlobalContext context = new GlobalContext();
            InternalLambda functionLambda = new InternalLambda
            {
                closures = new Closure[0],
                description = function.CompileInner(),
                context = context
            };
            // TODO: does the context need the function itself?
            return functionLambda;
        }
    }
}

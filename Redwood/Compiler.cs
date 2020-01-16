using Redwood.Ast;
using Redwood.Instructions;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

            if (variable.Global)
            {
                return;
            }
            else if (variable.Closured)
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

    internal class OverloadGroup
    {
        internal string name;
        internal List<FunctionDefinition> definitions;
        internal Variable variable;

        internal OverloadGroup(List<FunctionDefinition> definitions, Variable variable)
        {
            name = definitions[0].Name;
            this.definitions = definitions;
            this.variable = variable;
        }

        internal bool IsSingleMethod()
        {
            return definitions.Count == 1;
        }

        internal IEnumerable<Instruction> Compile()
        {
            if (definitions.Count == 1)
            {
                return definitions[0].Compile();
            }

            List<Instruction> instructions = new List<Instruction>();
            InternalLambdaDescription[] overloadDescriptions = definitions
                    .Select(function => function.CompileInner())
                    .ToArray();

            instructions.Add(new BuildInternalLambdasInstruction(overloadDescriptions));
            instructions.Add(Compiler.CompileVariableAssign(variable));

            return instructions;
        }

        internal void DoBind(Binder binder)
        {
            // Don't double-bind any non-overloaded methods
            if (definitions.Count > 1)
            {
                binder.BindVariable(variable);
            }
        }
    }

    public static class Compiler
    {
        internal static List<Assembly> assemblies = new List<Assembly>();

        public static void ExposeAssembly(Assembly assembly)
        {
            // TODO: how many assemblies could we possibly expect?
            if (!assemblies.Contains(assembly))
            {
                assemblies.Add(assembly);
            }
        }

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
                    if (freeVariables[i].InLVal)
                    {
                        variablesByName[name].Mutated = true;
                    }

                    freeVariables[i].Variable = variablesByName[name];
                    freeVariables.RemoveAt(i);
                }
            }
        }

        internal static List<OverloadGroup> GenerateOverloads(
            List<FunctionDefinition> functions)
        {

            Dictionary<string, List<FunctionDefinition>> functionsByName =
                new Dictionary<string, List<FunctionDefinition>>();

            foreach (FunctionDefinition function in functions)
            {
                if (functionsByName.ContainsKey(function.Name))
                {
                    functionsByName[function.Name].Add(function);
                }
                else
                {
                    functionsByName.Add(function.Name, new List<FunctionDefinition>(
                        new FunctionDefinition[] { function })
                    );
                }
            }

            List<OverloadGroup> overloads = new List<OverloadGroup>();
            foreach (List<FunctionDefinition> overload in functionsByName.Values)
            {
                // TODO: make the LambdaGroup a 
                Variable variable = overload.Count == 1 ?
                    overload[0].DeclaredVariable :
                    new Variable
                    {
                        Name = overload[0].Name,
                        KnownType = RedwoodType.GetForCSharpType(typeof(LambdaGroup))
                    };
                overloads.Add(new OverloadGroup(overload, variable));
            }
            return overloads;
        }

        internal static Instruction CompileVariableLookup(Variable variable)
        {
            if (variable.DefinedConstant && !variable.Mutated)
            {
                return new LoadConstantInstruction(variable.ConstantValue);
            }
            else if (variable.Global)
            {
                return new LookupGlobalInstruction(variable.Name);
            }
            else if (variable.Closured)
            {
                return new LookupClosureInstruction(variable.ClosureID, variable.Location);
            }
            else
            {
                return new LookupLocalInstruction(variable.Location);
            }
        }

        internal static Instruction CompileVariableAssign(Variable variable)
        {
            if (variable.Global)
            {
                return new AssignGlobalInstruction(variable.Name);
            }
            else if (variable.Closured)
            {
                return new AssignClosureInstruction(variable.ClosureID, variable.Location);
            }
            else
            {
                return new AssignLocalInstruction(variable.Location);
            }
        }


        public static GlobalContext CompileModule(TopLevel toplevel)
        {
            IEnumerable<NameExpression> freeVars = toplevel.Walk();
            if (freeVars.Count() > 0)
            {
                throw new NotImplementedException();
            }

            Binder binder = new Binder();
            binder.EnterFullScope();
            toplevel.Bind(binder);
            GlobalContext context = new GlobalContext();
            InternalLambda initializationLambda = new InternalLambda
            {
                closures = new Closure[0],
                description = new InternalLambdaDescription
                {
                    argTypes = new RedwoodType[0],
                    // All fields should go the global definition, but some
                    // temporary variables may be used to define constructors
                    stackSize = binder.LeaveFullScope(),
                    closureSize = 0,
                    instructions = toplevel.Compile().ToArray(),
                    returnType = null
                },
                context = context
            };
            // Populate the global context
            initializationLambda.Run();
            return context;
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

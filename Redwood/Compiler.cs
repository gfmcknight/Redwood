using Redwood.Ast;
using Redwood.Instructions;
using Redwood.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Redwood
{
    internal class Binder
    {
        private int scopeDepth;
        private List<int> maxStack;
        private List<int> stackPosition;
        private List<int> closurePosition;
        private List<RedwoodType> returnType;

        internal Binder()
        {
            scopeDepth = -1;
            maxStack = new List<int>();
            stackPosition = new List<int>();
            closurePosition = new List<int>();
            returnType = new List<RedwoodType>();
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
            closurePosition.RemoveAt(closurePosition.Count - 1);
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

        internal void PushReturnType(RedwoodType type)
        {
            returnType.Add(type);
        }

        internal void PopReturnType()
        {
            returnType.RemoveAt(returnType.Count - 1);
        }

        internal RedwoodType GetReturnType()
        {
            return returnType[returnType.Count - 1];
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
                variable.KnownType = variable.KnownType.GetGenericSpecialization(
                   definitions
                       .Select(
                           definition => definition.DeclaredVariable.KnownType
                       )
                       .ToArray()
               );
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

        internal static List<NameExpression> MatchVariables(
            List<NameExpression> freeVariables,
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
            return freeVariables;
        }

        static List<NameExpression> MatchWith(
            this List<NameExpression> self,
            IEnumerable<Variable> declaredVariables)
        {
            return Compiler.MatchVariables(self, declaredVariables);
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

            // TODO: Define a strict ordering for functions in an overload group
            List<OverloadGroup> overloads = new List<OverloadGroup>();
            foreach (List<FunctionDefinition> overload in functionsByName.Values)
            {
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

        internal static IEnumerable<Instruction> CompileImplicitConversion(
            RedwoodType from,
            RedwoodType to)
        {
            if (to == null)
            {
                // If we have a null destination, this shouldn't
                // even get called; we will be resolving it closer
                // to runtime
                throw new ArgumentException("Cannot convert to dynamic type");
            }

            if (from == null)
            {
                return new Instruction[]
                {
                    new DynamicConvertInstruction(to)
                };
            }
            else if (to.IsAssignableFrom(from))
            {
                // In this case, we are a subtype or the correct type
                // so we don't need to do anything
                return new Instruction[0];
            }
            else if (from.HasImplicitConversion(to))
            {
                if (from.CSharpType == null)
                {
                    if (!from.HasImplicitConversion(to))
                    {
                        throw new NotImplementedException();
                    }

                    int slot = from.implicitConversionMap[to];
                    return new Instruction[]
                    {
                        new LookupDirectMemberInstruction(slot),
                        new InternalCallInstruction(new int[0])
                    };
                }

                // This call won't work on a RedwoodType in the compile phase
                // because the static lambdas aren't populated yet
                return new Instruction[] {
                    new CallWithResultInstruction(
                        RuntimeUtil.GetConversionLambda(from, to)
                    )
                };
            }
            else
            {
                // Cannot convert between the types
                throw new NotImplementedException();
            }
        }

        private class ImportDetails
        {
            public string From { get; set; }
            public string To { get; set; }
            public string Name { get; set; }
            public Variable Variable { 
                get { return NameExpression.Variable; }
                set { NameExpression.Variable = value; } }
            public NameExpression NameExpression { get; set; }

            public ImportDetails(string module, NameExpression import)
            {
                To = module;
                int dotIndex = import.Name.LastIndexOf('.');
                From = import.Name.Substring(0, dotIndex);
                
                Name = import.Name.Substring(dotIndex + 1);
                NameExpression = import;
            }
        }

        private static List<Variable> GetSpecialMappedVariables()
        {
            List<Variable> builtinVariables = new List<Variable>();
            foreach (string name in RedwoodType.specialMappedTypes.Keys)
            {
                builtinVariables.Add(new Variable
                {
                    Name = name,
                    DefinedConstant = true,
                    ConstantValue = RedwoodType.specialMappedTypes[name],
                    KnownType = RedwoodType.GetForCSharpType(typeof(RedwoodType))
                });
            }
            return builtinVariables;
        }

        public static async Task<GlobalContext> CompileModule(
            TopLevel toplevel,
            IResourceProvider resources)
        {
            // We may have some 
            Dictionary<string, TopLevel> modules = new Dictionary<string, TopLevel>();
            modules[toplevel.ModuleName] = toplevel;

            List<Variable> specialMappedTypeVariables = GetSpecialMappedVariables();

            // If it has a dotwalk, it must be an import prefix
            List<ImportDetails> imports = toplevel
                .Walk()
                .ToList()
                .MatchWith(specialMappedTypeVariables)
                .Select((NameExpression ne) => new ImportDetails(toplevel.ModuleName, ne))
                .ToList();

            for (int i = 0; i < imports.Count; i++)
            {
                // Allow people to compile modules standalone without a
                // compiler, but don't let them compile if there are
                // unresolved modules
                if (resources == null)
                {
                    throw new NotImplementedException();
                }

                // If we haven't already got the module, then we
                // will have to go parse it ourselves
                if (!modules.ContainsKey(imports[i].From))
                {
                    // TODO: Other ways of caching modules?
                    if (!resources.TryGetResource(imports[i].From, out Stream stream))
                    {
                        throw new NotImplementedException();
                    }

                    StreamReader sr = new StreamReader(stream);
                    TopLevel tl = await new Parser(sr).ParseModule(imports[i].From);
                    imports.AddRange(
                        tl.Walk()
                          .ToList()
                          .MatchWith(specialMappedTypeVariables)
                          .Select((NameExpression ne) => new ImportDetails(imports[i].To, ne))
                    );
                    modules[imports[i].From] = tl;
                }

                Variable variable = modules[imports[i].From]
                        .DeclaredVariables
                        .FirstOrDefault(variable => variable.Name == imports[i].Name);
                
                if (variable == null)
                {
                    throw new NotImplementedException();
                }

                imports[i].Variable = variable;
            }

            // We need to bind twice in order to determine some of
            // the types, such as for a method in a class first,
            // then bind again for its usage
            foreach (TopLevel module in modules.Values)
            {
                module.Bind(new Binder());
            }
            if (toplevel.ModuleName == null)
            {
                toplevel.Bind(new Binder());
            }

            foreach (TopLevel module in modules.Values)
            {
                module.Bind(new Binder());
            }
            if (toplevel.ModuleName == null)
            {
                toplevel.Bind(new Binder());
            }

            Dictionary<string, GlobalContext> contexts =
                new Dictionary<string, GlobalContext>();
            foreach (TopLevel module in modules.Values)
            {
                contexts[module.ModuleName] = BuildContext(module);
            }

            // TODO: what if someone tries to import an import?
            foreach (ImportDetails import in imports)
            {
                contexts[import.To].AssignVariable(
                    import.NameExpression.Variable.Name,
                    contexts[import.From].LookupVariable(import.Name)
                );
            }
            return contexts[toplevel.ModuleName];

            GlobalContext BuildContext(TopLevel toplevel)
            {
                GlobalContext context = new GlobalContext();
                InternalLambda initializationLambda = new InternalLambda
                {
                    closures = new Closure[0],
                    description = new InternalLambdaDescription
                    {
                        argTypes = new RedwoodType[0],
                        // All fields should go the global definition, but some
                        // temporary variables may be used to define constructors
                        stackSize = toplevel.StackSize,
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

        }

        public static Lambda CompileFunction(FunctionDefinition function)
        {
            List<Variable> specialMappedTypes = GetSpecialMappedVariables();

            IEnumerable<NameExpression> freeVars = function
                .Walk()
                .ToList()
                .MatchWith(specialMappedTypes);

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

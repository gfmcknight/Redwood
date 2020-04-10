using Redwood.Ast;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Redwood.Runtime
{
    public class RedwoodType
    {
        internal static Dictionary<string, RedwoodType> specialMappedTypes =
            new Dictionary<string, RedwoodType>();
        internal static Dictionary<Type, RedwoodType> typeAdaptors = new Dictionary<Type, RedwoodType>();
        internal static Dictionary<ImmutableList<RedwoodType>, List<RedwoodType>> lambdaTypes =
            new Dictionary<ImmutableList<RedwoodType>, List<RedwoodType>>();
        public static RedwoodType Void = new RedwoodType();
        public static RedwoodType NullType = new RedwoodType();

        // Name of member/method -> slot number
        internal Dictionary<string, int> slotMap;
        // Slot number -> overloads
        internal Dictionary<int, Tuple<RedwoodType[][], int[]>> overloadsMap;
        // Type -> slot
        internal Dictionary<RedwoodType, int> implicitConversionMap;
        internal RedwoodType[] slotTypes;
        internal int numSlots;

        // Name of member/method -> slot number
        internal Dictionary<string, int> staticSlotMap;
        internal RedwoodType[] staticSlotTypes;
        internal Lambda[] staticLambdas;

        public string Name { get; private set; }
        public RedwoodType BaseType { get; private set; }
        public Type CSharpType { get; private set; }
        // TODO: Make this immutable?
        public RedwoodType[] GenericArguments { get; private set; }
        public RedwoodType NonGenericType { get; private set; }
        public Lambda Constructor { get; internal set; }

        static RedwoodType()
        {
            specialMappedTypes.Add("?", null);

            specialMappedTypes.Add("int", GetForCSharpType(typeof(int)));
            specialMappedTypes.Add("string", GetForCSharpType(typeof(string)));
            specialMappedTypes.Add("double", GetForCSharpType(typeof(double)));
            specialMappedTypes.Add("bool", GetForCSharpType(typeof(bool)));
            specialMappedTypes.Add("object", GetForCSharpType(typeof(object)));

            NullType.staticSlotMap = new Dictionary<string, int>();
            NullType.staticSlotMap[RuntimeUtil.NameForOperator(BinaryOperator.Equals)] = 0;
            NullType.staticSlotMap[RuntimeUtil.NameForOperator(BinaryOperator.NotEquals)] = 1;
            NullType.staticSlotTypes = new RedwoodType[]
            {
                RedwoodType.GetForLambdaArgsTypes(
                    typeof(InPlaceLambda),
                    RedwoodType.GetForCSharpType(typeof(bool)),
                    new RedwoodType[] { null , null }
                ),
                RedwoodType.GetForLambdaArgsTypes(
                    typeof(InPlaceLambda),
                    RedwoodType.GetForCSharpType(typeof(bool)),
                    new RedwoodType[] { null , null }
                )
            };

            NullType.staticLambdas = new Lambda[] {
                new InPlaceLambda(
                    new RedwoodType[] { null , null },
                    GetForCSharpType(typeof(bool)),
                    new InPlaceLambdaExecutor((stack, locs) => {
                        return stack[locs[0]] == null && stack[locs[1]] == null;
                    })
                ),
                new InPlaceLambda(
                    new RedwoodType[] { null , null },
                    GetForCSharpType(typeof(bool)),
                    new InPlaceLambdaExecutor((stack, locs) => {
                        return stack[locs[0]] != null || stack[locs[1]] != null;
                    })
                )
            };

        }

        public static RedwoodType GetForCSharpType(Type type)
        {
            if (!typeAdaptors.ContainsKey(type))
            {
                typeAdaptors[type] = new RedwoodType(type);
            }
            return typeAdaptors[type];
        }

        public bool IsAssignableFrom(Type type)
        {
            if (CSharpType != null)
            {
                return CSharpType.IsAssignableFrom(type);
            }
            return false;
        }

        public bool IsAssignableFrom(RedwoodType type)
        {
            // Any type of variable can be filled with null
            if (type == NullType)
            {
                return true;
            }

            if (GenericArguments != null)
            {
                if (type.GenericArguments != null &&
                    type.GenericArguments.Length != GenericArguments.Length)
                {
                    return false;
                }

                // Use referential equality since a RedwoodType should
                // only exist once with a certain set of generic type
                // arguments.
                for (int i = 0; i < GenericArguments.Length; i++)
                {
                    if (GenericArguments[i] != type.GenericArguments[i])
                    {
                        return false;
                    }
                }
            }
            if (type.CSharpType != null)
            {
                return IsAssignableFrom(type.CSharpType);
            }

            RedwoodType walker = this;
            // TODO: will this be problematic for inheriting generic
            // arguments?
            while (walker != null && walker != type)
            {
                walker = walker.BaseType;
            }

            return type == walker;
        }

        public bool IsAssignableFrom(object obj)
        {
            // All objects can be null
            if (obj == null)
            {
                return true;
            }

            if (obj is RedwoodObject rwo)
            {
                return IsAssignableFrom(rwo.Type);
            }
            return IsAssignableFrom(obj.GetType());
        }

        internal RedwoodType AncestorWithImplicitConversion(RedwoodType type)
        {
            int slot = implicitConversionMap[type];
            RedwoodType walker = this;
            while (walker.BaseType != null && walker.BaseType.numSlots > slot)
            {
                walker = walker.BaseType;
            }
            return walker;
        }

        public bool HasImplicitConversion(RedwoodType type)
        {
            if (CSharpType == null)
            {
                return implicitConversionMap.ContainsKey(type);
            }

            IEnumerable<MethodInfo> implicits =
                CSharpType.GetTypeInfo().GetDeclaredMethods("op_Implicit");

            return implicits.Any(info =>
                {
                    ParameterInfo[] parameters = info.GetParameters();
                    return parameters.Length == 1 &&
                           parameters[0].ParameterType != CSharpType &&
                           info.ReturnType == type.CSharpType;
                }
            );
        }

        public int AncestorCount(RedwoodType type)
        {
            RedwoodType walker = this;
            int count = 0;
            while (walker != null && walker != type)
            {
                count++;
                walker = walker.BaseType;
            }
            return count;
        }

        public bool IsPrimitiveType()
        {
            return CSharpType != null && IsPrimitiveType(CSharpType);
        }

        internal static bool IsPrimitiveType(Type type)
        {
            return type == typeof(int) ||
                   type == typeof(string) ||
                   type == typeof(bool) ||
                   type == typeof(double);
        }

        internal static bool TryGetSpecialMappedType(string name, out RedwoodType type)
        {
            return specialMappedTypes.TryGetValue(name, out type);
        }

        internal RedwoodType GetKnownTypeForStaticMember(string member)
        {
            if (staticSlotTypes == null)
            {
                return null;
            }

            if (!staticSlotMap.ContainsKey(member))
            {
                return null;
            }

            return staticSlotTypes[staticSlotMap[member]];
        }

        internal RedwoodType GetKnownTypeOfMember(string member)
        {
            if (slotTypes == null)
            {
                return null;
            }

            if (!slotMap.ContainsKey(member))
            {
                return null;
            }

            return slotTypes[slotMap[member]];
        }

        internal int GetSlotNumberForOverload(string functionName, RedwoodType[] argTypes)
        {
            int baseSlot = slotMap[functionName];
            RedwoodType[][] overloadArgTypes = overloadsMap[baseSlot].Item1;

            bool found = RuntimeUtil.TrySelectOverload(
                argTypes,
                overloadArgTypes,
                out int index);

            if (found)
            {
                return overloadsMap[baseSlot].Item2[index];
            }
            else
            {
                // Retrun a sentinel value so we can count the lambda as
                // not found.
                return -1;
            }
        }

        internal RedwoodType GetGenericSpecialization(RedwoodType[] genericArgs)
        {
            RedwoodType newType = new RedwoodType();
            newType.Name = Name;
            newType.CSharpType = CSharpType;
            // TODO: Base type with generic specialization?
            newType.BaseType = BaseType;
            newType.GenericArguments = genericArgs;
            newType.Constructor = Constructor;
            // TODO: Some of these will need generic specialization
            newType.implicitConversionMap = implicitConversionMap;
            newType.slotMap = slotMap;
            newType.slotTypes = slotTypes;
            newType.overloadsMap = overloadsMap;
            return newType;
        }

        internal static RedwoodType GetForLambdaArgsTypes(
            Type type, // ExternalLambda, InternalLambda, etc
            RedwoodType returnType,
            RedwoodType[] paramTypes)
        {
            List<RedwoodType> signature = new List<RedwoodType>();
            signature.AddRange(paramTypes);
            signature.Add(returnType);
            // This works because each RedwoodType is unique; referential equality
            // is all that is needed
            ImmutableList<RedwoodType> signatureImmutable = signature.ToImmutableList();
            if (lambdaTypes.ContainsKey(signatureImmutable))
            {
                RedwoodType res = lambdaTypes[signatureImmutable].FirstOrDefault(t => t.CSharpType == type);
                if (res != null)
                {
                    return res;
                }
            }
            else
            {
                lambdaTypes[signatureImmutable] = new List<RedwoodType>();
            }

            RedwoodType redwoodType = new RedwoodType();
            redwoodType.CSharpType = type;
            redwoodType.GenericArguments = signature.ToArray();
            return redwoodType;
        }

        private RedwoodType()
        {
            implicitConversionMap = new Dictionary<RedwoodType, int>();
            overloadsMap = new Dictionary<int, Tuple<RedwoodType[][], int[]>>();
            // TODO: RedwoodType for void only?
        }

        private RedwoodType(Type cSharpType)
        {
            CSharpType = cSharpType;
            Name = CSharpType.Name;

            Type[] genericArgs = cSharpType.GenericTypeArguments;
            GenericArguments = new RedwoodType[genericArgs.Length];

            for (int i = 0; i < genericArgs.Length; i++)
            {
                GenericArguments[i] = GetForCSharpType(genericArgs[i]);
            }
            
            // TODO: base type's generic arguments?
            if (cSharpType != typeof(object) && cSharpType.BaseType != null)
            {
                BaseType = GetForCSharpType(cSharpType.BaseType);
            }

            // TODO: Is LINQ too slow for a runtime context?
            Constructor = RuntimeUtil.CanonicalizeLambdas(
                cSharpType
                    .GetConstructors()
                    .Select(constructor => new ExternalLambda(this, constructor))
                    .ToArray()
            );
        }

        internal static RedwoodType Make(ClassDefinition @class)
        {
            RedwoodType type = new RedwoodType();
            type.Name = @class.Name;
            // TODO: Number of slots?
            return type;
        }
    }
}

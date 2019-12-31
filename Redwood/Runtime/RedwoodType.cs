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
        internal static Dictionary<Type, RedwoodType> typeAdaptors = new Dictionary<Type, RedwoodType>();
        internal static Dictionary<ImmutableList<RedwoodType>, List<RedwoodType>> lambdaTypes =
            new Dictionary<ImmutableList<RedwoodType>, List<RedwoodType>>();
        public static RedwoodType Void = new RedwoodType();

        // Name of member/method -> slot number
        internal Dictionary<string, int> slotMap;
        // Slot number -> overloads
        internal Dictionary<int, InternalLambdaDescription[]> overloadsMap;
        // Type -> slot
        internal Dictionary<RedwoodType, int> implicitConversionMap;
        internal RedwoodType[] slotTypes;
        internal int numSlots;

        public RedwoodType BaseType { get; private set; }
        public Type CSharpType { get; private set; }
        // TODO: Make this immutable?
        public RedwoodType[] GenericArguments { get; private set; }
        public RedwoodType NonGenericType { get; private set; }
        public Lambda Constructor { get; internal set; }

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

        internal static bool TryGetPrimitiveFromName(string name, out RedwoodType type)
        {
            // TODO: This needs a better structure than just 
            // a massive switch statement to populate all types
            switch (name)
            {
                case "int":
                    type = GetForCSharpType(typeof(int));
                    return true;
                case "string":
                    type = GetForCSharpType(typeof(string));
                    return true;
                case "bool":
                case "boolean":
                    type = GetForCSharpType(typeof(bool));
                    return true;
                case "?": // TODO: Oh boy!
                    type = null;
                    return true;
                default:
                    type = null;
                    return false;
            }
        }

        internal int GetSlotNumberForOverload(string functionName, RedwoodType[] argTypes)
        {
            int baseSlot = slotMap[functionName];
            InternalLambdaDescription[] overloads = overloadsMap[baseSlot];
            RedwoodType[][] overloadArgTypes = new RedwoodType[overloads.Length][];
            for (int i = 0; i < overloads.Length; i++)
            {
                overloadArgTypes[i] = overloads[i].argTypes;
            }

            bool found = RuntimeUtil.TrySelectOverload(
                argTypes,
                overloadArgTypes,
                out int actualSlot);

            if (found)
            {
                return actualSlot;
            }
            else
            {
                // TODO!
                throw new NotImplementedException("Handling of unfound slots");
            }
        }

        internal static RedwoodType GetForLambdaArgsTypes(
            Type type,
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
            // TODO: RedwoodType for void only?
        }

        private RedwoodType(Type cSharpType)
        {
            CSharpType = cSharpType;

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
        }

        internal static RedwoodType Make(ClassDefinition @class)
        {
            RedwoodType type = new RedwoodType();
            // TODO: Number of slots?
            return type;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Runtime
{
    public class RedwoodType
    {
        internal static Dictionary<Type, RedwoodType> typeAdaptors = new Dictionary<Type, RedwoodType>();
        public static RedwoodType Void = new RedwoodType();

        // Name of member/method -> slot number
        internal Dictionary<string, int> slotMap;
        // Slot number -> overloads
        internal Dictionary<int, InternalLambdaDescription[]> overloadsMap;
        // Type -> slot
        internal Dictionary<RedwoodType, int> implicitConversionMap;
        internal int numSlots;

        public RedwoodType BaseType { get; set; }
        public Type CSharpType { get; set; }

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
            if (type.CSharpType != null)
            {
                return IsAssignableFrom(type.CSharpType);
            }
            // TODO
            return type == this;
        }

        public bool IsAssignableFrom(object obj)
        {
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
            return implicitConversionMap.ContainsKey(type);
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

        private RedwoodType()
        {
            // TODO: RedwoodType for void only?
        }

        private RedwoodType(Type cSharpType)
        {
            CSharpType = cSharpType;
            if (cSharpType != typeof(object))
            {
                BaseType = GetForCSharpType(cSharpType.BaseType);
            }
        }
    }
}

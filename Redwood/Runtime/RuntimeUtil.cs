using Redwood.Ast;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Redwood.Runtime
{
    internal static class RuntimeUtil
    {
        internal static string NameForOperator(BinaryOperator op)
        {
            switch(op)
            {
                case BinaryOperator.Multiply:
                    return "op_Multiply";
                case BinaryOperator.Divide:
                    return "op_Division";
                case BinaryOperator.Modulus:
                    return "op_Modulus";
                case BinaryOperator.Add:
                    return "op_Addition";
                case BinaryOperator.Subtract:
                    return "op_Subtraction";
                case BinaryOperator.LeftShift:
                    return "op_LeftShift";
                case BinaryOperator.RightShift:
                    return "op_RightShift";
                case BinaryOperator.LessThan:
                    return "op_LessThan";
                case BinaryOperator.GreaterThan:
                    return "op_GreaterThan";
                case BinaryOperator.LessThanOrEquals:
                    return "op_LessThanOrEqual";
                case BinaryOperator.GreaterThanOrEquals:
                    return "op_GreaterThanOrEqual";
                case BinaryOperator.Equals:
                    return "op_Equality";
                case BinaryOperator.NotEquals:
                    return "op_Inequality";
                case BinaryOperator.BitwiseAnd:
                    return "op_BitwiseAnd";
                case BinaryOperator.BitwiseXor:
                    return "op_ExclusiveOr";
                case BinaryOperator.BitwiseOr:
                    return "op_BitwiseOr";
                case BinaryOperator.LogicalAnd:
                case BinaryOperator.LogicalOr:
                case BinaryOperator.Coalesce:
                case BinaryOperator.Assign:
                default:
                    // These operators don't have names because
                    // they're skipping some evaluations/controlled
                    // by flow
                    return null;
            }
        }

        internal static string GetNameOfConversionToType(string typename)
        {
            if (RedwoodType.TryGetSpecialMappedType(typename, out RedwoodType type))
            {
                return "as_" + type.Name;
            }

            return "as_" + typename;
        }

        internal static RedwoodType[] GetTypesFromMethodInfo(MethodInfo methodInfo)
        {
            ParameterInfo[] parameters = methodInfo.GetParameters();
            RedwoodType[] types = new RedwoodType[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                types[i] = RedwoodType.GetForCSharpType(parameters[i].ParameterType);
            }
            return types;
        }

        internal static bool TryConvertToLambda(object o, out Lambda lambda)
        {
            // TODO: Other ways of making a lambda out of an object?
            lambda = o as Lambda;
            return lambda != null;
        }

        internal static void SelectBestOverloads(
            RedwoodType[] args,
            RedwoodType[][] overloads,
            /* out */ bool[] candidate)
        {
            // From left to right, we will compare the conversion distance
            // needed to get to 
            int bestMatchIndex = -1;
            int[] bestMatch = new int[args.Length];
            int[] currentMatch = new int[args.Length];


            for (int i = 0; i < overloads.Length; i++)
            {
                candidate[i] = true;
                RedwoodType[] overload = overloads[i];
                if (overload.Length != args.Length)
                {
                    candidate[i] = false;
                    continue;
                }

                for (int j = 0; j < overload.Length; j++)
                {
                    if (args[j] == null)
                    {
                        // Once we don't have a known type, quit
                        break;
                    }
                    else if (overload[j] == null || 
                             overload[j].IsAssignableFrom(args[j]))
                    {
                        currentMatch[j] = 2 * args[j].AncestorCount(overload[j]);
                    }
                    else if (args[j].HasImplicitConversion(overload[j]))
                    {
                        currentMatch[j] = 1 + 2 * args[j].AncestorCount(
                            args[j].AncestorWithImplicitConversion(overload[j]));
                    }
                    else
                    {
                        candidate[i] = false;
                        break;
                    }
                }

                // Check if we are the new best match
                if (!candidate[i])
                {
                    continue;
                }

                bool isBestMatch = bestMatchIndex == -1;
                for (int j = 0; j < currentMatch.Length; j++)
                {
                    if (currentMatch[j] < bestMatch[j])
                    {
                        isBestMatch = true;
                    }

                    if (isBestMatch)
                    {
                        bestMatch[j] = currentMatch[j];
                    }
                    else if (currentMatch[j] > bestMatch[j])
                    {
                        // Not a better match, the other method matched
                        // better, earlier
                        candidate[i] = false;
                        break;
                    }
                }
                
                // If we've found a new best candidate, eliminate
                // all of the others
                if (isBestMatch && candidate[i])
                {
                    bestMatchIndex = i;
                    for (int j = i - 1; j >= 0; j--)
                    {
                        candidate[j] = false;
                    }
                }
            }
        }

        private static Lambda SelectOverloads(RedwoodType[] args, Lambda lambda, bool single)
        {
            RedwoodType[][] overloads;
            List<Lambda> lambdas = new List<Lambda>();

            if (lambda is LambdaGroup e)
            {
                overloads = new RedwoodType[e.lambdas.Length][];
                for (int i = 0; i < overloads.Length; i++)
                {
                    overloads[i] = e.lambdas[i].ExpectedArgs.ToArray();
                    lambdas.Add(e.lambdas[i]);
                }
            }
            else
            {
                overloads = new RedwoodType[][] { lambda.ExpectedArgs.ToArray() };
                lambdas.Add(lambda);
            }
            bool[] candidates = new bool[overloads.Length];
            SelectBestOverloads(args, overloads, candidates);


            for (int i = candidates.Length - 1; i >= 0; i--)
            {
                if (!candidates[i])
                {
                    lambdas.RemoveAt(i);
                }
            }

            if (single)
            {
                return CanonicalizeLambdas(lambdas.FirstOrDefault());
            }
            return CanonicalizeLambdas(lambdas.ToArray());
        }

        internal static RedwoodType[] GetTypesFromArgs(object[] args)
        {
            RedwoodType[] types = new RedwoodType[args.Length];
            for (int i = 0; i < types.Length; i++)
            {
                if (args[i] is RedwoodObject o)
                {
                    types[i] = o.Type;
                }
                else
                {
                    types[i] = RedwoodType.GetForCSharpType(args[i].GetType());
                }
            }
            return types;
        }

        internal static Lambda SelectOverloads(RedwoodType[] args, Lambda lambda)
        {
            return SelectOverloads(args, lambda, false);
        }

        internal static Lambda SelectSingleOverload(RedwoodType[] args, Lambda lambda)
        {
            return SelectOverloads(args, lambda, true);
        }

        internal static bool TrySelectOverload(RedwoodType[] args, RedwoodType[][] overloads, out int index)
        {

            bool[] candidate = new bool[overloads.Length];
            SelectBestOverloads(args, overloads, candidate);
            for (int i = 0; i < candidate.Length; i++)
            {
                // Just pick the first overload we get that's a best candidate
                if (candidate[i])
                {
                    index = i;
                    return true;
                }
            }
            index = -1;
            return false;
        }

        internal static Lambda CanonicalizeLambdas(params Lambda[] lambdas)
        {
            List<Lambda> flattened = new List<Lambda>();
            for (int i = 0; i < lambdas.Length; i++)
            {
                if (lambdas[i] == null)
                {
                    continue;
                }
                else if (lambdas[i] is LambdaGroup g)
                {
                    flattened.AddRange(g.lambdas);
                }
                else
                {
                    flattened.Add(lambdas[i]);
                }
            }

            if (flattened.Count == 0)
            {
                return null;
            }
            else if (flattened.Count == 1)
            {
                return flattened[0];
            }
            else
            {
                return new LambdaGroup(flattened.ToArray());
            }
        }

        internal static Lambda GetConversionLambda(RedwoodType from, RedwoodType to)
        {
            if (from.CSharpType == null)
            {
                // TODO: RedwoodType implicit conversions
                throw new NotImplementedException();
            }
            else if (to.CSharpType == null)
            {
                // CSharp type -> Redwood type
                // This shouldn't happen, I think?
                throw new NotImplementedException();
            }
            else
            {
                IEnumerable<MethodInfo> implicits =
                    from.CSharpType.GetTypeInfo().GetDeclaredMethods("op_Implicit");
                MethodInfo conversion = implicits
                    .Where(method => method.ReturnType == to.CSharpType)
                    .FirstOrDefault();

                if (conversion == null)
                {
                    throw new NotImplementedException();
                }

                return new ExternalLambda(null, conversion);
            }
        }

        internal static RedwoodType GetTypeOf(object o)
        {
            if (o == null)
            {
                return null;
            }
            else if (o is RedwoodObject rwo)
            {
                return rwo.Type;
            }
            else
            {
                return RedwoodType.GetForCSharpType(o.GetType());
            }
        }

        internal static int[] GetSlotMapsToInterface(RedwoodType from, RedwoodType to)
        {
            // We have to map to an interface, otherwise we're just
            // shoving things where they don't belong
            if (!to.IsInterface)
            {
                throw new NotImplementedException();
            }

            // We want to fill up every slot up to the overloads, which are
            // packed at the end of the class
            // TODO: This is a bad way of counting overloads. It checks whether
            // a given overload index is "self referential", meaning that it's the
            // only option, in which case it would not constitute a "LambdaGroup"
            int numOverloads = to.overloadsMap
                .Where(kv => kv.Key != kv.Value.Item2[0])
                .Count();
            int[] slotsToUse = new int[to.numSlots - numOverloads];

            foreach (KeyValuePair<string, int> member in to.slotMap)
            {
                string name = member.Key;
                int slot = member.Value;

                RedwoodType slotType = to.slotTypes[slot];
                if (slotType != null && slotType.CSharpType == typeof(LambdaGroup))
                {
                    Tuple<RedwoodType[][], int[]> overloadSlots = to.overloadsMap[slot];
                    for (int i = 0; i < overloadSlots.Item2.Length; i++)
                    {
                        int overloadMemberSlot = overloadSlots.Item2[i];
                        RedwoodType[] overloadArgTypes = overloadSlots.Item1[i];

                        slotsToUse[overloadMemberSlot] = from.GetSlotNumberForOverload(name, overloadArgTypes);
                    }
                }
                else if (slotType != null && typeof(Lambda).IsAssignableFrom(slotType.CSharpType))
                {
                    slotsToUse[slot] = from.GetSlotNumberForOverload(
                        name,
                        slotType.GenericArguments
                            .SkipLast(1)
                            .ToArray()
                    );
                }
                else
                {
                    // TODO: This case should only be hit for the this keyword
                    slotsToUse[slot] = from.slotMap[name];
                }
            }

            return slotsToUse;
        }
    }
}

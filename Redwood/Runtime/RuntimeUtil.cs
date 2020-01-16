using Redwood.Ast;
using System;
using System.Collections.Generic;
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
                    return "op_GreaterThanOrEqual";
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
    }
}

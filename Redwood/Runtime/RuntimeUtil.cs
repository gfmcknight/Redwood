using System;
using System.Collections.Generic;
using System.Text;

namespace Redwood.Runtime
{
    internal static class RuntimeUtil
    {
        internal static bool TryConvertToLambda(object o, out Lambda lambda)
        {
            // TODO: Other ways of making a lambda out of an object?
            lambda = o as Lambda;
            return lambda != null;
        }

        internal static bool TrySelectOverload(RedwoodType[] args, RedwoodType[][] overloads, out int index)
        {
            
            // From left to right, we will compare the conversion distance
            // needed to get to 
            int bestMatchIndex = -1;
            int[] bestMatch = new int[args.Length];
            int[] currentMatch = new int[args.Length];


            for (int i = 0; i < overloads.Length; i++)
            {
                RedwoodType[] overload = overloads[i];
                if (overload.Length != args.Length)
                {
                    continue;
                }

                bool isValid = true;
                for (int j = 0; j < overload.Length; j++)
                {
                    if (overload[i].IsAssignableFrom(args[i]))
                    {
                        currentMatch[i] = 2 * args[i].AncestorCount(overload[i]);
                    }
                    else if (args[i].HasImplicitConversion(overload[i]))
                    {
                        currentMatch[i] = 1 + 2 * args[i].AncestorCount(
                            args[i].AncestorWithImplicitConversion(overload[i]));
                    }
                    else
                    {
                        isValid = false;
                        break;
                    }
                }

                // Check if we are the new best match
                if (isValid)
                {
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
                            break;
                        }
                    }
                }
            }
            // TODO: Actually select one
            index = bestMatchIndex;
            return bestMatchIndex != -1;
        }
    }
}

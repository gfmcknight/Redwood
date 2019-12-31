using Redwood.Ast;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Redwood.Runtime
{
    internal class MethodGroup
    {
        internal MethodInfo[] infos;

        internal MethodGroup(MethodInfo[] infos)
        {
            this.infos = infos;
        }

        internal void SelectOverloads(RedwoodType[] argumentTypes)
        {
            bool[] candidates = new bool[infos.Length];
            RedwoodType[][] overloads = new RedwoodType[infos.Length][];
            for (int i = 0; i < infos.Length; i++)
            {
                overloads[i] = RuntimeUtil.GetTypesFromMethodInfo(infos[i]);
            }
            RuntimeUtil.SelectBestOverloads(argumentTypes, overloads, candidates);

            List<MethodInfo> selectedInfos = new List<MethodInfo>();
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i])
                {
                    selectedInfos.Add(infos[i]);
                }
            }
            infos = selectedInfos.ToArray();
        }
    }

    internal static partial class MemberResolver
    {
        internal static bool TryResolve(
            object target, 
            RedwoodType targetTypeHint,
            string name, 
            out object result)
        {
            if (TryResolveMember(target, targetTypeHint, name, target == null, out result))
            {
                return true;
            }

            if (TryResolveMethod(target, targetTypeHint, name, false, out MethodInfo[] group))
            {
                result = new LambdaGroup(target, group);
                return true;
            }

            result = null;
            return false;
        }

        internal static bool TryResolveLambda(
            object target,
            RedwoodType targetTypeHint,
            string name,
            out Lambda result)
        {
            bool @static = target == null || target is RedwoodType;


            if (TryResolveMethod(target, targetTypeHint, name, target == null, out MethodInfo[] group))
            {
                if (group.Length == 1)
                {
                    result = new ExternalLambda(target, group[0]);
                }
                else
                {
                    result = new LambdaGroup(target, group);
                }
                return true;
            }

            if (TryResolveMember(target, targetTypeHint, name, target == null, out object member))
            {
                return RuntimeUtil.TryConvertToLambda(member, out result);
            }

            result = null;
            return false;
        }

        internal static bool TryResolveMethod(
            object target,
            RedwoodType targetTypeHint,
            string name,
            bool @static,
            out MethodInfo[] result)
        {
            BindingFlags flags = @static ?
                (BindingFlags.Public | BindingFlags.Static) : 
                (BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            Type type = targetTypeHint?.CSharpType ?? target.GetType();
            MethodInfo[] methods = type
                .GetTypeInfo()
                .GetMethods(flags)
                .Where(method => method.Name == name)
                .ToArray();

            if (methods.Length > 0)
            {
                result = methods;
                return true;
            }

            result = null;
            return false;
        }

        internal static void TryResolveMember(
            RedwoodType targetTypeHint,
            string name,
            bool @static,
            out PropertyInfo property,
            out FieldInfo field)
        {
            BindingFlags flags = @static ?
                (BindingFlags.Public | BindingFlags.Static) :
                (BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            Type type = targetTypeHint?.CSharpType;
            TypeInfo typeInfo = type.GetTypeInfo();

            property = typeInfo.GetProperty(name, flags);
            field = typeInfo.GetField(name, flags);
        }

        internal static bool TryResolveMember(
            object target,
            RedwoodType targetTypeHint,
            string name,
            bool @static,
            out object result)
        {
            BindingFlags flags = @static ?
                (BindingFlags.Public | BindingFlags.Static) :
                (BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

            Type type = targetTypeHint?.CSharpType ?? target.GetType();
            TypeInfo typeInfo = type.GetTypeInfo();

            PropertyInfo property = typeInfo.GetProperty(name, flags);

            if (property != null)
            {
                result = property.GetValue(target);
                return true;
            }

            FieldInfo field = typeInfo.GetField(name, flags); ;
            if (field != null)
            {
                result = field.GetValue(target);
                return true;
            }

            result = null;
            return false;
        }

        internal static bool TryResolveOperator(
            object left,
            object right,
            RedwoodType leftTypeHint,
            RedwoodType rightTypeHint,
            BinaryOperator op,
            out Lambda lambda)
        {
            Type leftType = leftTypeHint?.CSharpType ?? left.GetType();
            Type rightType = rightTypeHint?.CSharpType ?? right.GetType();

            Lambda leftOperators;
            Lambda rightOperators;

            if (RedwoodType.IsPrimitiveType(leftType))
            {
                primitiveOperators[leftType].TryGetValue(new OperatorDescriptor(op), out leftOperators);
            }
            else
            {
                TryResolveLambda(left, leftTypeHint, RuntimeUtil.NameForOperator(op), out leftOperators);
            }
            
            if (leftTypeHint == rightTypeHint)
            {
                rightOperators = null;
            }
            else if (RedwoodType.IsPrimitiveType(rightType))
            {
                primitiveOperators[rightType].TryGetValue(new OperatorDescriptor(op), out rightOperators);
            }
            else
            {
                TryResolveLambda(right, rightTypeHint, RuntimeUtil.NameForOperator(op), out rightOperators);
            }

            lambda = RuntimeUtil.CanonicalizeLambdas(leftOperators, rightOperators);
            lambda = RuntimeUtil.SelectSingleOverload(
                new RedwoodType[] { leftTypeHint, rightTypeHint }, 
                lambda
            );

            return lambda != null;
        }
    }
}

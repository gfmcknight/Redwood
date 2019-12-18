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

        public MethodGroup(MethodInfo[] infos)
        {
            this.infos = infos;
        }
    }

    internal static class MemberResolver
    {
        internal static bool TryResolve(
            object target, 
            RedwoodType targetTypeHint,
            string name, 
            out object result)
        {
            if (TryResolveMember(target, targetTypeHint, name, out result))
            {
                return true;
            }

            if (TryResolveMethod(target, targetTypeHint, name, out MethodInfo[] group))
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
            if (TryResolveMethod(target, targetTypeHint, name, out MethodInfo[] group))
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

            if (TryResolveMember(target, targetTypeHint, name, out object member))
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
            out MethodInfo[] result)
        {
            Type type = targetTypeHint?.CSharpType ?? target.GetType();
            MethodInfo[] methods = type.GetTypeInfo().GetDeclaredMethods(name).ToArray();

            if (methods.Length > 0)
            {
                result = methods;
                return true;
            }

            result = null;
            return false;
        }

        internal static bool TryResolveMember(
            object target,
            RedwoodType targetTypeHint,
            string name,
            out object result)
        {
            Type type = targetTypeHint?.CSharpType ?? target.GetType();
            TypeInfo typeInfo = type.GetTypeInfo();
            
            PropertyInfo property = typeInfo.GetDeclaredProperty(name);
            if (property != null)
            {
                result = property.GetValue(target);
                return true;
            }

            FieldInfo field = typeInfo.GetDeclaredField(name);
            if (field != null)
            {
                result = field.GetValue(target);
                return true;
            }

            result = null;
            return false;
        }
    }
}

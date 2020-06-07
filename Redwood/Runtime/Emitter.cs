using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;

namespace Redwood.Runtime
{
    internal class Emitter
    {
        private static AssemblyName assembly = new AssemblyName("RedwoodInterfaceAssembly");
        private static AssemblyBuilder assemblyBuilder =
            AssemblyBuilder.DefineDynamicAssembly(
                assembly,
                AssemblyBuilderAccess.Run
            );
        private static ModuleBuilder interfaceModules = assemblyBuilder.DefineDynamicModule("interfaces");

        internal static Type EmitInterfaceProxyType(RedwoodType type, Type @interface)
        {
            TypeBuilder tb = interfaceModules.DefineType(
                @interface.Name + "RedwoodProxy",
                TypeAttributes.Public | TypeAttributes.AutoClass | TypeAttributes.AnsiClass,
                typeof(object),
                new Type[] { @interface });

            FieldBuilder fb = tb.DefineField("proxy", typeof(object[]), FieldAttributes.Private);

            ConstructorBuilder cb = tb.DefineConstructor(
                MethodAttributes.Public |
                MethodAttributes.HideBySig |
                MethodAttributes.SpecialName | 
                MethodAttributes.RTSpecialName,
                CallingConventions.HasThis,
                new Type[] { typeof(object[]) }
            );


            ILGenerator constructorGenerator = cb.GetILGenerator();
            // Call our base constructor
            ConstructorInfo objectConstructor = typeof(object).GetConstructor(new Type[] { });
            constructorGenerator.Emit(OpCodes.Ldarg_0);
            constructorGenerator.Emit(OpCodes.Call, objectConstructor);

            // Load this, then load arg 1, then set the local field
            constructorGenerator.Emit(OpCodes.Ldarg_0);
            constructorGenerator.Emit(OpCodes.Ldarg_1);
            constructorGenerator.Emit(OpCodes.Stfld, fb);

            constructorGenerator.Emit(OpCodes.Ret);

            MethodInfo runMethod = typeof(Lambda).GetMethod("Run");

            foreach (MethodInfo method in @interface.GetMethods())
            {
                Type[] paramTypes = method
                        .GetParameters()
                        .Select(param => param.ParameterType)
                        .ToArray();

                // The method is on an interface so it is guaranteed
                // to have the abstract tag so we have to reverse that
                MethodBuilder mb = tb.DefineMethod(
                    method.Name,
                    method.Attributes ^ MethodAttributes.Abstract,
                    method.ReturnType,
                    paramTypes
                );
                int numberOfArguments = method.GetParameters().Length;

                // The index into our proxy object array holding the overload
                int index = type.GetSlotNumberForOverload(
                    method.Name,
                    paramTypes
                        .Select(type => RedwoodType.GetForCSharpType(type))
                        .ToArray()
                );

                ILGenerator generator = mb.GetILGenerator();
                generator.DeclareLocal(typeof(int));

                // Let's load the proxy object because we're going
                // to be accessing on it a little later
                generator.Emit(OpCodes.Ldarg_0);
                generator.Emit(OpCodes.Ldfld, fb);

                // Now go fishing for our overload
                generator.Emit(OpCodes.Ldc_I4, index);
                generator.Emit(OpCodes.Ldelem_Ref);
                // ILSpy says we do this check now -- I guess the actual throw
                // is handled by the runtime?
                generator.Emit(OpCodes.Isinst, typeof(Lambda));

                // Create an array to pass as the params argument 
                generator.Emit(OpCodes.Ldc_I4, numberOfArguments);
                generator.Emit(OpCodes.Newarr, typeof(object));

                // Set each argument on the params array
                for (int i = 0; i < numberOfArguments; i++)
                {
                    // Get a copy of the array
                    generator.Emit(OpCodes.Dup);
                    // Index
                    generator.Emit(OpCodes.Ldc_I4, i);
                    // Element
                    generator.Emit(OpCodes.Ldarg, i + 1);

                    // Since we're going into an object array, we need
                    // to box our value types
                    if (paramTypes[i].IsValueType)
                    {
                        // TODO: is there a separate boxed type from value type?
                        generator.Emit(OpCodes.Box, paramTypes[i]);
                    }

                    // Set
                    generator.Emit(OpCodes.Stelem_Ref);
                }

                generator.Emit(OpCodes.Callvirt, runMethod);
                if (method.ReturnType.IsValueType)
                {
                    generator.Emit(OpCodes.Unbox_Any, method.ReturnType);
                }

                // For some reason, the compiler likes to store and load before
                // returning, so lets do it here too
                // TODO: does this look different for non-value types?
                generator.Emit(OpCodes.Stloc_0);
                generator.Emit(OpCodes.Ldloc_0);

                generator.Emit(OpCodes.Ret);
            }

            return tb.CreateType();
        }
    }
}

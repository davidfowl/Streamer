using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace Streamer
{
    internal static class TypedChannelBuilder<T>
    {
        private const string clientModule = "Streamer.TypedChannel";

        // There is one static instance of _builder per T
        private static Lazy<Func<ClientChannel, T>> _builder = new Lazy<Func<ClientChannel, T>>(() => GenerateChannelBuilder());

        private static ChannelMethods _channelMethods = new ChannelMethods();

        public static T Build(ClientChannel channel)
        {
            return _builder.Value(channel);
        }

        public static void Validate()
        {
            // The following will throw if T is not a valid type
            var forceEvaluation = _builder.Value;
        }

        private static Func<ClientChannel, T> GenerateChannelBuilder()
        {
            VerifyInterface();

            var assemblyName = new AssemblyName(clientModule);
            AssemblyBuilder assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(clientModule);
            Type clientType = GenerateInterfaceImplementation(moduleBuilder);

            return proxy => (T)Activator.CreateInstance(clientType, proxy);
        }

        private static Type GenerateInterfaceImplementation(ModuleBuilder moduleBuilder)
        {
            TypeBuilder type = moduleBuilder.DefineType(typeof(T).Name + "Impl", TypeAttributes.Public,
                typeof(Object), new Type[] { typeof(T) });

            FieldBuilder proxyField = type.DefineField("_channel", typeof(ClientChannel), FieldAttributes.Private);

            BuildConstructor(type, proxyField);

            foreach (var method in typeof(T).GetMethods())
            {
                BuildMethod(type, method, proxyField);
            }

            return type.CreateType();
        }

        private static void BuildConstructor(TypeBuilder type, FieldInfo channelField)
        {
            MethodBuilder method = type.DefineMethod(".ctor", System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.HideBySig);

            ConstructorInfo ctor = typeof(object).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new Type[] { }, null);

            method.SetReturnType(typeof(void));
            method.SetParameters(typeof(ClientChannel));

            ILGenerator generator = method.GetILGenerator();

            // Call object constructor
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Call, ctor);

            // Assign constructor argument to the proxyField
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Stfld, channelField);
            generator.Emit(OpCodes.Ret);
        }

        private static void BuildMethod(TypeBuilder type, MethodInfo interfaceMethodInfo, FieldInfo proxyField)
        {
            MethodAttributes methodAttributes =
                  MethodAttributes.Public
                | MethodAttributes.Virtual
                | MethodAttributes.Final
                | MethodAttributes.HideBySig
                | MethodAttributes.NewSlot;

            ParameterInfo[] parameters = interfaceMethodInfo.GetParameters();
            Type[] paramTypes = parameters.Select(param => param.ParameterType).ToArray();

            MethodBuilder methodBuilder = type.DefineMethod(interfaceMethodInfo.Name, methodAttributes, typeof(void), paramTypes);

            var hasReturnValue = interfaceMethodInfo.ReturnType != typeof(Task);
            var genericReturnType = hasReturnValue ? interfaceMethodInfo.ReturnType.GetGenericArguments()[0] : null;

            MethodInfo invokeMethod = !hasReturnValue ?
                                        _channelMethods.InvokeNoResult :
                                        _channelMethods.InvokeWithResult.MakeGenericMethod(genericReturnType);

            methodBuilder.SetReturnType(interfaceMethodInfo.ReturnType);
            methodBuilder.SetParameters(paramTypes);

            ILGenerator generator = methodBuilder.GetILGenerator();

            // Declare local variable to store the arguments to ClientChannel.Invoke
            generator.DeclareLocal(typeof(object[]));

            // Get IClientProxy
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldfld, proxyField);

            // The first argument to ClientChannel.Invoke is this method's name
            generator.Emit(OpCodes.Ldstr, ComputeRPCMethodName(interfaceMethodInfo));

            // Create an new object array to hold all the parameters to this method
            generator.Emit(OpCodes.Ldc_I4, parameters.Length);
            generator.Emit(OpCodes.Newarr, typeof(object));
            generator.Emit(OpCodes.Stloc_0);

            // Store each parameter in the object array
            for (int i = 0; i < paramTypes.Length; i++)
            {
                generator.Emit(OpCodes.Ldloc_0);
                generator.Emit(OpCodes.Ldc_I4, i);
                generator.Emit(OpCodes.Ldarg, i + 1);
                generator.Emit(OpCodes.Box, paramTypes[i]);
                generator.Emit(OpCodes.Stelem_Ref);
            }

            // Call IProxy.Invoke
            generator.Emit(OpCodes.Ldloc_0);
            generator.Emit(OpCodes.Callvirt, invokeMethod);

            if (interfaceMethodInfo.ReturnType == typeof(void))
            {
                // void return
                generator.Emit(OpCodes.Pop);
            }

            generator.Emit(OpCodes.Ret);
        }

        private static string ComputeRPCMethodName(MethodInfo interfaceMethodInfo)
        {
            // Namespace.IBlah.MethodAsync = Namespace.Blah.Method
            var methodName = interfaceMethodInfo.Name;

            if (methodName.EndsWith("Async"))
            {
                methodName = methodName.Substring(0, methodName.Length - "Async".Length);
            }

            return typeof(T).Namespace + "." + typeof(T).Name.TrimStart('I') + "." + methodName;
        }

        private static void VerifyInterface()
        {
            var interfaceType = typeof(T);

            if (!interfaceType.IsInterface)
            {
                throw new NotSupportedException("Only interfaces are supported");
            }

            if (interfaceType.GetProperties().Length != 0)
            {
                throw new NotSupportedException("Properties are not supported");
            }

            if (interfaceType.GetEvents().Length != 0)
            {
                throw new NotSupportedException("Events are not supported");
            }

            foreach (var method in interfaceType.GetMethods())
            {
                VerifyMethod(interfaceType, method);
            }
        }

        private static void VerifyMethod(Type interfaceType, MethodInfo interfaceMethod)
        {
            if (!IsTaskReturningMethod(interfaceMethod))
            {
                throw new NotSupportedException("Method must return Task or Task<T>");
            }

            foreach (var parameter in interfaceMethod.GetParameters())
            {
                VerifyParameter(interfaceType, interfaceMethod, parameter);
            }
        }

        private static bool IsTaskReturningMethod(MethodInfo interfaceMethod)
        {
            return interfaceMethod.ReturnType == typeof(Task) ||
                   (interfaceMethod.ReturnType.IsGenericType && interfaceMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
        }

        private static void VerifyParameter(Type interfaceType, MethodInfo interfaceMethod, ParameterInfo parameter)
        {
            if (parameter.IsOut)
            {
                throw new NotSupportedException("Out parameters are not supported");
            }

            if (parameter.ParameterType.IsByRef)
            {
                throw new NotSupportedException("Ref parameters are not supported");
            }
        }

        private class ChannelMethods
        {
            public MethodInfo InvokeNoResult { get; private set; }
            public MethodInfo InvokeWithResult { get; private set; }

            public ChannelMethods()
            {
                var invokeMethods = typeof(ClientChannel).GetMethods().Where(m => m.Name == "InvokeAsync");

                // There's only 2 invoke methods

                InvokeNoResult = invokeMethods.Single(m => !m.IsGenericMethod);
                InvokeWithResult = invokeMethods.Single(m => m.IsGenericMethod);
            }
        }
    }
}

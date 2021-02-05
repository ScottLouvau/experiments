// Copyright (c) Microsoft Corporation. All rights reserved.
// Modified from https://github.com/microsoft/bion/blob/main/csharp/BSOA/RoughBench/BenchmarkReflector.cs.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Copy
{
    public static class Reflector
    {
        public static Dictionary<string, WithSignature> BenchmarkMethods<WithSignature>(Type fromType, object instance = null)
        {
            Dictionary<string, WithSignature> methods = new Dictionary<string, WithSignature>();

            // Identify the return type and argument types on the desired method signature
            Type delegateOrFuncType = typeof(WithSignature);
            MethodInfo withSignatureInfo = delegateOrFuncType.GetMethod("Invoke");
            Type returnType = withSignatureInfo.ReturnType;
            List<Type> arguments = new List<Type>(withSignatureInfo.GetParameters().Select((pi) => pi.ParameterType));

            // Find all public methods with 'Benchmark' attribute and correct signature
            foreach (MethodInfo method in fromType.GetMethods(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
            {
                if (!method.GetCustomAttributes().Where((a) => a.GetType().Name == "BenchmarkAttribute").Any()) { continue; }
                if (!method.ReturnType.Equals(returnType)) { continue; }
                if (!arguments.SequenceEqual(method.GetParameters().Select((pi) => pi.ParameterType))) { continue; }

                if (!method.IsStatic && instance == null)
                {
                    // Create an instance of the desired class (triggering any initialization)
                    instance = fromType.GetConstructor(new Type[0]).Invoke(null);
                }

                methods[method.Name] = (WithSignature)(object)method.CreateDelegate(delegateOrFuncType, instance);
            }

            return methods;
        }
    }
}

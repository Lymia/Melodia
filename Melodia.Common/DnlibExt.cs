namespace Melodia.Common;

using dnlib.DotNet;
using dnlib.DotNet.Emit;

public static class DnlibAssemblyExt {
    /// <summary>
    /// Imports a reference to a method which may contain circular references.
    /// </summary>
    public static IMethod ImportHook(this AssemblyDef assembly, AssemblyDef hook, string module, string method) {
        var hookModule = hook.Find(module, false);
        var hookMethod = hookModule.FindMethod(method);

        var shimMethodName = $"MelodiaTrampoline$${method}";
        var shimMethod = hookModule.FindMethod(shimMethodName);
        if (shimMethod != null) {
            if (shimMethod.MethodSig != hookMethod.Signature) throw new System.Exception($"Duplicate hook method '{shimMethodName}'??");
        } else {
            var cleanedSig = hookMethod.MethodSig.Clone();
            for (int i = 0; i < cleanedSig.Params.Count; i++) {
                if (cleanedSig.Params[i].DefinitionAssembly.FullName == assembly.FullName)
                    cleanedSig.Params[i] = hookModule.Module.CorLibTypes.Object;
            }

            var newMethod = new MethodDefUser(
                shimMethodName, cleanedSig, 
                MethodImplAttributes.IL | MethodImplAttributes.AggressiveInlining, 
                MethodAttributes.Public | (hookMethod.IsStatic ? MethodAttributes.Static : 0)
            );
            newMethod.Body = new CilBody();
            newMethod.PushMethodArgs();
            newMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(hookMethod));
            newMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            newMethod.DeclaringType = hookModule;

            shimMethod = hookModule.FindMethod(shimMethodName);
        }

        return new Importer(assembly.ManifestModule).Import(shimMethod);
    }
}

public static class DnlibMethodExt {
    internal static void PushMethodArgs(this MethodDef method, int head = -1, bool omitThis = false) {
        if (head == -1) head = method.Body.Instructions.Count;
        for (int i = (omitThis && !method.IsStatic ? 1 : 0); i < method.Parameters.Count; i++) {
            if (i == 0) method.Body.Instructions.Insert(head, OpCodes.Ldarg_0.ToInstruction());
            else if (i == 1) method.Body.Instructions.Insert(head, OpCodes.Ldarg_1.ToInstruction());
            else if (i == 2) method.Body.Instructions.Insert(head, OpCodes.Ldarg_2.ToInstruction());
            else if (i == 3) method.Body.Instructions.Insert(head, OpCodes.Ldarg_3.ToInstruction());
            else if (i < 256) method.Body.Instructions.Insert(head, OpCodes.Ldarg_S.ToInstruction(i));
            else method.Body.Instructions.Insert(head, OpCodes.Ldarg.ToInstruction(i));

            head += 1;
        }
    }
}
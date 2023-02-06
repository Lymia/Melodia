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
        if (!hookMethod.IsStatic) throw new System.Exception("Hook methods must be static!");

        var shimMethodName = $"MelodiaHook__{method}";
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
                MethodAttributes.Public | MethodAttributes.Static
            );
            newMethod.Body = new CilBody();
            newMethod.Body.MaxStack = (ushort) cleanedSig.Params.Count;
            for (int i = 0; i < cleanedSig.Params.Count; i++) {
                if (i == 0) newMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                else if (i == 1) newMethod.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
                else if (i == 2) newMethod.Body.Instructions.Add(OpCodes.Ldarg_2.ToInstruction());
                else if (i == 3) newMethod.Body.Instructions.Add(OpCodes.Ldarg_3.ToInstruction());
                else if (i < 256) newMethod.Body.Instructions.Add(OpCodes.Ldarg_S.ToInstruction(i));
                else newMethod.Body.Instructions.Add(OpCodes.Ldarg.ToInstruction(i));
            }
            newMethod.Body.Instructions.Add(OpCodes.Call.ToInstruction(hookMethod));
            newMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            newMethod.DeclaringType = hookModule;
            shimMethod = hookModule.FindMethod(shimMethodName);
        }

        return new Importer(assembly.ManifestModule).Import(shimMethod);
    }
}

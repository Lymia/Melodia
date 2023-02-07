namespace Melodia.Common.Plugin;

using System;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;

[AttributeUsage(AttributeTargets.Class)]
public class PatchAttribute : Attribute {  
    private string hookTarget;  
  
    public PatchAttribute(string hookTarget) {
        this.hookTarget = hookTarget;  
    }  
}

[AttributeUsage(AttributeTargets.Class)]
public class PatchComponentAttribute : Attribute {}

[AttributeUsage(AttributeTargets.Method)]
public class ReplaceMethodAttribute : Attribute {  
    private string hookTarget;  
    public bool CallBase;
    public ReplaceMethodAttribute(string hookTarget) {
        this.hookTarget = hookTarget;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class CallAfterAttribute : Attribute {  
    private string hookTarget;
    public CallAfterAttribute(string hookTarget) {
        this.hookTarget = hookTarget;
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class CallBeforeAttribute : Attribute {  
    private string hookTarget;
    public CallBeforeAttribute(string hookTarget) {
        this.hookTarget = hookTarget;
    }
}

internal interface PatcherAction {
    internal readonly record struct ReplaceMethod(FieldDef storageField, MethodDef patchMethod, bool callBase) : PatcherAction;
    internal readonly record struct CallBefore(FieldDef storageField, MethodDef patchMethod) : PatcherAction;
    internal readonly record struct CallAfter(FieldDef storageField, MethodDef patchMethod) : PatcherAction;
}

public static class DnlibTypeDefPatcherExt {
    public static FieldDef GetTypePatchField(this TypeDef type, Importer importer, TypeDef patchType) {
        var fieldName = $"MelodiaPatch$${patchType.FullName.Replace(".", "$")}";
        var newFieldSig = importer.Import(new FieldSig(patchType.ToTypeSig()));

        var existingField = type.FindField(new UTF8String(fieldName), newFieldSig);
        if (existingField != null) {
            return existingField;
        } else {
            // Add a new field for this type
            var newField = new FieldDefUser(new UTF8String(fieldName), newFieldSig);
            newField.Attributes = FieldAttributes.Public | FieldAttributes.CompilerControlled | FieldAttributes.InitOnly;
            newField.DeclaringType = type;

            // Add a new static constructor method for this type
            var constructorName = $"MelodiaConstructorTrampoline$${patchType.FullName.Replace(".", "$")}";
            var newConstructorMethod = new MethodDefUser(
                constructorName, new MethodSig(CallingConvention.Default, 0, patchType.ToTypeSig(), patchType.Module.CorLibTypes.Object), 
                MethodImplAttributes.IL | MethodImplAttributes.AggressiveInlining, 
                MethodAttributes.Public | MethodAttributes.Static
            );
            newConstructorMethod.Body = new CilBody();
            newConstructorMethod.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            newConstructorMethod.Body.Instructions.Add(OpCodes.Newobj.ToInstruction(patchType.FindConstructors().First()));
            newConstructorMethod.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
            newConstructorMethod.DeclaringType = patchType;
        
            // Initialize the new field in all constructors
            foreach (var constructor in type.FindConstructors()) {
                if (constructor.IsIL) {
                    if (constructor.Body.Instructions.Last().GetOpCode() != OpCodes.Ret)
                        throw new Exception("Method body does not end in ret??");

                    constructor.Body.Instructions.InsertRange(constructor.Body.Instructions.Count - 1, new Instruction [] {
                        OpCodes.Ldarg_0.ToInstruction(),
                        OpCodes.Ldarg_0.ToInstruction(),
                        OpCodes.Call.ToInstruction(importer.Import(newConstructorMethod)),
                        OpCodes.Stfld.ToInstruction(newField),
                    });
                }
            }

            // return the newly defined field
            return newField;
        }
    }
}

public static class PatcherContextProcessAttrs {
    private static void ApplyPatches(
        PatcherContext ctx, Importer importer, TypeDef targetType, 
        AssemblyDef patchAssembly, TypeDef patchParentType, TypeDef patchCodeType
    ) {
        // Add a new field for this type
        var fieldName = $"MelodiaPatch$${patchParentType.FullName.Replace(".", "$")}";
        var newFieldSig = importer.Import(new FieldSig(patchParentType.ToTypeSig()));
        var newField = targetType.GetTypePatchField(importer, patchParentType);

        // Apply all patch methods
        foreach (var method in patchCodeType.Methods) {
            var attr = method.CustomAttributes.Find(importer.Import(typeof(ReplaceMethodAttribute)));
            if (attr != null) {
                var targetName = ((UTF8String) attr.ConstructorArguments[0].Value).ToString();
                var targetMethod = targetType.FindMethod(targetName);
                var callBaseRaw = attr.GetField("CallBase");
                var callBase = callBaseRaw == null ? false : (bool) callBaseRaw.Argument.Value;
                ctx.addPatchAction(targetMethod, new PatcherAction.ReplaceMethod(newField, method, callBase));
            }

            attr = method.CustomAttributes.Find(importer.Import(typeof(CallBeforeAttribute)));
            if (attr != null) {
                var targetName = ((UTF8String) attr.ConstructorArguments[0].Value).ToString();
                var targetMethod = targetType.FindMethod(targetName);
                ctx.addPatchAction(targetMethod, new PatcherAction.CallBefore(newField, method));
            }

            attr = method.CustomAttributes.Find(importer.Import(typeof(CallAfterAttribute)));
            if (attr != null) {
                var targetName = ((UTF8String) attr.ConstructorArguments[0].Value).ToString();
                var targetMethod = targetType.FindMethod(targetName);
                ctx.addPatchAction(targetMethod, new PatcherAction.CallAfter(newField, method));
            }
        }
    }

    public static void ApplyPatches(this PatcherContext ctx, string targetAssemblyName, string patchAssemblyName) {
        Log.Debug($"     - Loading patches from assembly '{patchAssemblyName}'...");

        var targetAssembly = ctx.LoadAssembly(targetAssemblyName);
        var patchAssembly = ctx.LoadAssembly(patchAssemblyName);
        ctx.MarkAssemblyModified(targetAssemblyName);
        ctx.MarkAssemblyModified(patchAssemblyName);

        var importer = new Importer(targetAssembly.ManifestModule);
        foreach (var module in patchAssembly.Modules) {
            foreach (var type in module.Types) {
                if (type.IsClass) {
                    var attr = type.CustomAttributes.Find(importer.Import(typeof(PatchAttribute)));
                    if (attr != null) {
                        var curType = type;
                        while (curType != null) {
                            var targetName = ((UTF8String) attr.ConstructorArguments[0].Value).ToString();
                            var targetType = targetAssembly.Find(targetName, false);
                            if (targetType == null) throw new Exception($"Class '{targetName}' not found!");
                            ApplyPatches(ctx, importer, targetType, patchAssembly, type, curType);
                            
                            var parentClassRef = curType.GetBaseType();
                            if (parentClassRef.FullName.Equals("System.Object")) break;
                            
                            var parentAssembly = ctx.LoadAssembly(parentClassRef.DefinitionAssembly.Name);
                            if (parentAssembly == null) break;

                            var parentClass = parentAssembly.Find(parentClassRef.FullName, false);
                            if (parentClass.CustomAttributes.Find(importer.Import(typeof(PatchComponentAttribute))) == null) break;

                            curType = parentClass;
                        }
                    }
                }
            }
        }
    }

    private static void commitPatchesMethod(this PatcherContext ctx, AssemblyDef assembly, Importer importer, MethodDef method) {
        var actions = ctx.patchActions[method];
        var funcIsVoid = method.ReturnType == method.Module.CorLibTypes.Void;

        PatcherAction.ReplaceMethod? replacement = null;
        foreach (var rawAction in actions) {
            if (rawAction is PatcherAction.ReplaceMethod action) {
                if (replacement != null) {
                    throw new Exception($"Duplicate replacement patch over '{method}': '{replacement.Value.patchMethod}' and '{action.patchMethod}'");
                }
                replacement = action;
            }
        }
        if (replacement != null) {
            var action = replacement.Value;

            method.Body = new CilBody();
            if (action.callBase) {                    
                var superClass = assembly.Find(method.DeclaringType.GetBaseType(true).FullName, false);
                var baseCall = superClass.FindMethod(method.Name, method.MethodSig);

                method.PushMethodArgs();
                method.Body.Instructions.Add(OpCodes.Call.ToInstruction(importer.Import(baseCall)));
                if (!funcIsVoid) method.Body.Instructions.Add(OpCodes.Pop.ToInstruction());
            }

            var patchMethod = action.patchMethod;
            var hookMethod = method.Module.Assembly.ImportHook(patchMethod.Module.Assembly, patchMethod.DeclaringType.FullName, patchMethod.Name);
            method.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
            method.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(action.storageField));
            method.PushMethodArgs(-1, true);
            method.Body.Instructions.Add(OpCodes.Call.ToInstruction(hookMethod));
        } else {
            if (method.Body.Instructions.Last().GetOpCode() != OpCodes.Ret)
                throw new Exception("Method body does not end in ret??");
            method.Body.Instructions.RemoveAt(method.Body.Instructions.Count - 1);
        }

        var hasCallAfter = false;
        foreach (var rawAction in actions) {
            if (rawAction is PatcherAction.CallBefore action) {
                var patchMethod = action.patchMethod;
                var hookMethod = method.Module.Assembly.ImportHook(patchMethod.Module.Assembly, patchMethod.DeclaringType.FullName, patchMethod.Name);
                method.Body.Instructions.Insert(0, OpCodes.Ldarg_0.ToInstruction());
                method.Body.Instructions.Insert(1, OpCodes.Ldfld.ToInstruction(action.storageField));
                method.Body.Instructions.Insert(2, OpCodes.Call.ToInstruction(hookMethod));
                if (action.patchMethod.ReturnType != action.patchMethod.Module.CorLibTypes.Void)
                    method.Body.Instructions.Insert(3, OpCodes.Pop.ToInstruction());
                method.PushMethodArgs(2, true);
            }
            if (rawAction is PatcherAction.CallAfter _) {
                hasCallAfter = true;
            }
        }

        if (hasCallAfter) {
            if (!funcIsVoid) {
                method.Body.Variables.Add(new Local(method.ReturnType));
                method.Body.Instructions.Add(OpCodes.Stloc_S.ToInstruction(method.Body.Variables.Last()));
            }
            foreach (var rawAction in actions) {
                if (rawAction is PatcherAction.CallAfter action) {
                    var patchMethod = action.patchMethod;
                    var hookMethod = method.Module.Assembly.ImportHook(patchMethod.Module.Assembly, patchMethod.DeclaringType.FullName, patchMethod.Name);
                    method.Body.Instructions.Add(OpCodes.Ldarg_0.ToInstruction());
                    method.Body.Instructions.Add(OpCodes.Ldfld.ToInstruction(action.storageField));
                    method.PushMethodArgs(-1, true);
                    method.Body.Instructions.Add(OpCodes.Call.ToInstruction(hookMethod));
                    if (action.patchMethod.ReturnType != action.patchMethod.Module.CorLibTypes.Void)
                        method.Body.Instructions.Add(OpCodes.Pop.ToInstruction());
                }
            }
            if (!funcIsVoid)
                method.Body.Instructions.Add(OpCodes.Ldloc_S.ToInstruction(method.Body.Variables.Last()));
        }
        method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
    }
    internal static void commitPatches(this PatcherContext ctx, AssemblyDef assembly) {
        var importer = new Importer(assembly.ManifestModule);
        foreach (var module in assembly.Modules) {
            foreach (var type in module.Types) {
                var anyMethodsModified = false;
                foreach (var method in type.Methods) {
                    if (ctx.patchActions.ContainsKey(method)) {
                        anyMethodsModified = true;
                        break;
                    }
                }

                if (anyMethodsModified) {
                    Log.Debug($"     - Patching type '{type.Name}'...");
                    foreach (var method in type.Methods) {
                        if (ctx.patchActions.ContainsKey(method)) {
                            Log.Debug($"       - Patching method '{method.FullName}'...");
                            commitPatchesMethod(ctx, assembly, importer, method);
                        }
                    }
                }
            }
        }
    }
}

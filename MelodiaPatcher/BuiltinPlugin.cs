using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Melodia.Common;

namespace Melodia.Patcher;

internal sealed class BuiltinPlugin : Plugin {
    public override string DisplayName => "Melodia Framework";

    public override string DisplayVersion => Program.VersionString;

    public override string DisplayAuthor => "AuroraAmissa 2023";
    
    public override bool InvalidatesAchievements => false;

    private const string RestartAppIfNecessary = 
        "System.Boolean Steamworks.SteamAPI::RestartAppIfNecessary(Steamworks.AppId_t)";
    private static void DisableSteamRelaunch(AssemblyDef assembly, TypeDef callbacks)
    {
        var imp = new Importer(assembly.ManifestModule);
        var type = assembly.Find("Sang.Utility.SteamManager", false);
        var method = type.FindMethod("Initialize");

        for (int i = 0; i < method.Body.Instructions.Count; i++)
        {
            if (method.Body.Instructions[i].OpCode != OpCodes.Call) continue;
            var target = (IMethod)method.Body.Instructions[i].Operand;
            if (target.FullName != RestartAppIfNecessary) continue;
            method.Body.Instructions[i].Operand = imp.Import(callbacks.FindMethod("Hook_RestartAppIfNecessary"));

            break;
        }
    }
    
    private static void HookTitleVersion(AssemblyDef assembly, TypeDef callbacks)
    {
        var imp = new Importer(assembly.ManifestModule);
        var type = assembly.Find("Sang.Window.Title.WindowTitleFooter", false);
        var method = type.FindMethod("Draw");

        method.Body.Instructions.RemoveAfter(2);
        method.Body.Instructions.AddRange(new Instruction[] {
            OpCodes.Ldarg_0.ToInstruction(),
            OpCodes.Call.ToInstruction(imp.Import(callbacks.FindMethod("Hook_WindowTitleFooter_Draw"))),
            OpCodes.Ret.ToInstruction(),
        });
    }

    public override void Patch(PatcherContext context) {
        var assembly = context.LoadAssembly("Crystal Project");

        var patchAssembly = context.LoadAssembly("Melodia.CoreCallbacks");
        var callbacks = patchAssembly.ManifestModule.Find("Melodia.CoreCallbacks.Callbacks", false);

        Log.Debug("     - Disabling Steam Relaunch...");
        DisableSteamRelaunch(assembly, callbacks);

        Log.Debug("     - Hooking title version rendering...");
        HookTitleVersion(assembly, callbacks);

        context.MarkAssemblyModified("Crystal Project");
    }
}

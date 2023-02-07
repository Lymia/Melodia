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

    private const string Callbacks = "Melodia.CoreCallbacks.Callbacks";

    private const string RestartAppIfNecessary = 
        "System.Boolean Steamworks.SteamAPI::RestartAppIfNecessary(Steamworks.AppId_t)";
    private static void DisableSteamRelaunch(AssemblyDef assembly, AssemblyDef patch)
    {
        var type = assembly.Find("Sang.Utility.SteamManager", false);
        var method = type.FindMethod("Initialize");

        for (int i = 0; i < method.Body.Instructions.Count; i++)
        {
            if (method.Body.Instructions[i].OpCode != OpCodes.Call) continue;
            var target = (IMethod)method.Body.Instructions[i].Operand;
            if (target.FullName != RestartAppIfNecessary) continue;
            method.Body.Instructions[i].Operand = assembly.ImportHook(patch, Callbacks, "Hook_RestartAppIfNecessary");

            break;
        }
    }
    
    public override void Patch(PatcherContext context) {
        context.ApplyPatches("Crystal Project", "Melodia.CoreCallbacks");

        // additional manual patches - TODO: Make this no longer manual
        var assembly = context.LoadAssembly("Crystal Project");
        var patch = context.LoadAssembly("Melodia.CoreCallbacks");

        Log.Debug("     - Disabling Steam Relaunch...");
        DisableSteamRelaunch(assembly, patch);

        context.MarkAssemblyModified("Crystal Project");
        context.MarkAssemblyModified("Melodia.CoreCallbacks");
    }
}

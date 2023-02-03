using System;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using Melodia.Common;

namespace Melodia.Patcher;

internal sealed class BuiltinPlugin : Plugin {
    public override bool InvalidatesAchievements => false;

    private const string RestartAppIfNecessary = 
        "System.Boolean Steamworks.SteamAPI::RestartAppIfNecessary(Steamworks.AppId_t)";
    private static void DisableSteamRelaunch(AssemblyDef assembly, TypeDef callbacks)
    {
        var imp = new Importer(assembly.ManifestModule);
        var type = assembly.ManifestModule.Find("Sang.Utility.SteamManager", false);
        
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
    
    private static void sillysillyfairy(AssemblyDef assembly)
    {
        // this is temporary, in case you're wondering -w-
        // it's just here so it's obvious when it works

        var imp = new Importer(assembly.ManifestModule);
        var type = assembly.ManifestModule.Find("Sang.Window.Title.WindowTitleFooter", false);
        
        var method = type.FindMethod("Draw");

        for (int i = 0; i < method.Body.Instructions.Count; i++)
        {
            if (method.Body.Instructions[i].OpCode != OpCodes.Ldstr) continue;
            var target = (string)method.Body.Instructions[i].Operand;
            Console.WriteLine(target);
            Console.WriteLine("Andrew Willman 2017-2022");
            Console.WriteLine(target == "Andrew Willman 2017-2022");
            if (target != "Andrew Willman 2017-2022") continue;

            method.Body.Instructions[i].Operand = (String) "Lymia was here :D XD :3";

            break;
        }
    }

    public override void Patch(PatcherContext context) {
        var assembly = context.LoadAssembly("Crystal Project");

        var patchAssembly = context.LoadAssembly("Melodia.CoreCallbacks");
        var callbacks = patchAssembly.ManifestModule.Find("Melodia.CoreCallbacks.Callbacks", false);

        Log.Debug("     - Disabling Steam Relaunch...");
        DisableSteamRelaunch(assembly, callbacks);
        sillysillyfairy(assembly);

        context.MarkAssemblyModified("Crystal Project");
    }
}
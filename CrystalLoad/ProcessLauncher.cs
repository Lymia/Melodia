namespace CrystalLoad;

using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Threading;

// TODO: Redo the loading so we can have references from CrystalLoad.exe / other dlls into stuff properly.

internal class LoaderOptions {
    public readonly string GameDirectory;

    public bool AchievementsEnabled = true;

    public LoaderOptions(string gameDirectory)
    {
        GameDirectory = gameDirectory;
    }
}

internal static class ProcessLauncher {
    private static readonly NamedPermissionSet FULL_TRUST = new NamedPermissionSet("FullTrust");

    private static AssemblyDef LoadAssembly(string gameDirectory, string name) {
        var path = Path.Combine(gameDirectory, name);
        var expectedName = Path.GetFileNameWithoutExtension(name);
        var assembly = AssemblyDef.Load(path);
        Trace.Assert(assembly.Name == expectedName, $"{name} does not contain the correct assembly!");
        return assembly;
    }

    private static void AddOverride(TargetDomainCallback callback, AssemblyDef assembly, string? assemblyName = null) {
        Log.Debug($" - Adding patched assembly for {assembly.Name}");

        var settings = new ModuleWriterOptions(assembly.ManifestModule);

        var patchedData = new MemoryStream();
        assembly.Write(patchedData, settings);
        callback.AddOverride(assemblyName ?? assembly.Name, patchedData.ToArray());
    }

    private static readonly string RestartAppIfNecessary = 
        "System.Boolean Steamworks.SteamAPI::RestartAppIfNecessary(Steamworks.AppId_t)";
    private static void DisableSteamRelaunch(AssemblyDef assembly)
    {
        var imp = new Importer(assembly.ManifestModule);
        var type = assembly.ManifestModule.Find("Sang.Utility.SteamManager", false);
        
        var method = type.FindMethod("Initialize");

        for (int i = 0; i < method.Body.Instructions.Count; i++)
        {
            if (method.Body.Instructions[i].OpCode != OpCodes.Call) continue;
            var target = (IMethod)method.Body.Instructions[i].Operand;
            if (target.FullName != RestartAppIfNecessary) continue;

            method.Body.Instructions[i].Operand = imp.Import(typeof(Callbacks).GetMethod("RestartAppIfNecessary"));

            break;
        }
    }

    private static void DisableSteamAchievements(AssemblyDef assembly)
    {
        var imp = new Importer(assembly.ManifestModule);
        var type = assembly.ManifestModule.Find("Sang.Utility.SteamManager", false);
        
        var method = type.FindMethod("SetAchievement");

        // replace body with `return true;`
        method.Body = new CilBody { MaxStack = 1 };
        method.Body.Instructions.Add(OpCodes.Ldarg_1.ToInstruction());
        method.Body.Instructions.Add(OpCodes.Ret.ToInstruction());
    }
    
    private static void sillysillyfairy(AssemblyDef assembly)
    {
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

    private static void PatchApplication(LoaderOptions options, TargetDomainCallback callback)
    {
        var assembly = LoadAssembly(options.GameDirectory, "Crystal Project.exe");

        Log.Debug("   - Disabling Steam Relaunch...");
        DisableSteamRelaunch(assembly);

        if (!options.AchievementsEnabled) {
            Log.Debug("   - Disabling achievements...");
            DisableSteamAchievements(assembly);
        }

        sillysillyfairy(assembly);

        AddOverride(callback, assembly, "Crystal Project Patched");
    }

    public static Thread StartProcess(LoaderOptions options, string[] args)
    {
        Log.Info("Preparing to start Crystal Project");
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        Log.Debug(" - Creating application domain.");
        var setup = new AppDomainSetup
        {
            ApplicationBase = AppDomain.CurrentDomain.BaseDirectory,
            ApplicationName = "Crystal Project",
            DisallowCodeDownload = true,
            DisallowPublisherPolicy = true
        };
        setup.PrivateBinPath = setup.ApplicationBase;
        setup.PrivateBinPathProbe = "true";
        var appDomain = AppDomain.CreateDomain("Crystal Project", null, setup, FULL_TRUST);

        Log.Debug(" - Creating remote callback.");
        var callbackObj = appDomain.CreateInstance("CrystalLoad", typeof(TargetDomainCallback).FullName);
        var callback = (TargetDomainCallback) callbackObj.Unwrap();

        callback.Init(options.GameDirectory, Log.RemoteReceiver);

        Log.Debug(" - Patching Crystal Project");
        PatchApplication(options, callback);

        Log.Debug(" - Setting up environment.");
        Environment.CurrentDirectory = options.GameDirectory;

        stopwatch.Stop();
        Log.Debug($"Finished in {stopwatch.ElapsedMilliseconds} ms");
        Log.Debug();

        Log.Info("Launching Crystal Project");
        var thread = new Thread(() => {
            appDomain.ExecuteAssemblyByName("Crystal Project Patched", args);
            Log.Debug("Crystal Project terminated.");
        });
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        return thread;
    }
}

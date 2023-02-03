namespace Melodia.Patcher;

using Melodia.Common;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using System;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Threading;
using System.Collections.Generic;
using System.Reflection;

internal sealed class LoaderOptions {
    public readonly string GameDirectory;
    public readonly string BaseDirectory;
    public readonly List<Plugin> Plugins = new List<Plugin>();

    internal string[] PluginPath => new string[] { 
        Path.Combine(BaseDirectory, "lib/modules_guest"),
        GameDirectory,
    };

    public LoaderOptions(string gameDirectory, string baseDirectory)
    {
        GameDirectory = gameDirectory;
        BaseDirectory = baseDirectory;
    }

    public LoaderOptions AddPlugin(Plugin plugin) {
        this.Plugins.Add(plugin);
        return this;
    }

    public bool CheckAchivementsEnabled() {
        foreach (var plugin in Plugins) {
            if (plugin.InvalidatesAchievements) return false;
        }
        return true;
    }
}

internal sealed class TargetDomainCallback : PersistantRemoteObject {
    private string? gameDirectory;
    private PatcherContext patcherContext = new PatcherContext(new string[0]);
    private Dictionary<string, byte[]> patchedAssemblies = new Dictionary<string, byte[]>();
    private readonly Assembly myAssembly = Assembly.GetAssembly(typeof(TargetDomainCallback));

    internal void Init(string gameDirectory, LogDomainSynchronizer logLocal) {
        this.gameDirectory = gameDirectory;
        Log.Synchronizer.InitializeFromDomain(logLocal);
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
    }
    internal void CommitPatcher(PatcherContext context) {
        this.patchedAssemblies = context.CommitModified();
        this.patcherContext = context;
    }

    private Assembly? ResolveAssembly(Object sender, ResolveEventArgs ev) {
        Log.Debug($"Handling ResolveAssembly event for {ev.Name}");
        
        if (ev.Name == myAssembly.FullName) {
            Log.Debug(" - Using callback assembly");
            return myAssembly;
        }

        string name = ev.Name.Split(',')[0].Trim();

        if (patchedAssemblies.ContainsKey(name)) {
            Log.Debug(" - Using patched assembly");
            return Assembly.Load(patchedAssemblies[name]);
        }

        var path = patcherContext.FindAssemblyPath(name);
        if (path != null) return Assembly.LoadFrom(path);

        return null;
    }
}

internal static class ProcessLauncher {
    private static readonly NamedPermissionSet FULL_TRUST = new NamedPermissionSet("FullTrust");

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

    public static Thread StartProcess(LoaderOptions options, string[] args)
    {
        Log.Info("Preparing to start Crystal Project");
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        Log.Debug(" - Creating application domain.");
        var setup = new AppDomainSetup
        {
            ApplicationBase = options.GameDirectory,
            ApplicationName = "Crystal Project",
            DisallowCodeDownload = true,
            DisallowPublisherPolicy = true
        };
        setup.PrivateBinPath = AppDomain.CurrentDomain.BaseDirectory;
        setup.PrivateBinPathProbe = "true";
        var appDomain = AppDomain.CreateDomain("Crystal Project", null, setup, FULL_TRUST);

        Log.Debug(" - Creating remote callback.");
        var callbackObj = appDomain.CreateInstance(Program.AssemblyNameString, typeof(TargetDomainCallback).FullName);
        var callback = (TargetDomainCallback) callbackObj.Unwrap();

        callback.Init(options.GameDirectory, Log.Synchronizer);

        Log.Debug(" - Patching executables.");
        var patchContext = new PatcherContext(options.PluginPath);
        foreach (var plugin in options.Plugins) {
            Log.Debug($"   - Running plugin {plugin.GetType().FullName}.");
            plugin.Patch(patchContext);
        }

        // We hardcode this here to avoid having to do the mess of passing this information on to the plugins.
        if (!options.CheckAchivementsEnabled()) {
            DisableSteamAchievements(patchContext.LoadAssembly("Crystal Project"));
            patchContext.MarkAssemblyModified("Crystal Project");
        }

        callback.CommitPatcher(patchContext);

        Log.Debug(" - Setting up environment.");
        Environment.CurrentDirectory = options.GameDirectory;

        stopwatch.Stop();
        Log.Debug($"Finished in {stopwatch.ElapsedMilliseconds} ms");
        Log.Debug();

        Log.Info("Launching Crystal Project");
        var thread = new Thread(() => {
            appDomain.ExecuteAssemblyByName("Crystal Project", args);
            Log.Debug("Crystal Project terminated.");
        });
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        return thread;
    }
}

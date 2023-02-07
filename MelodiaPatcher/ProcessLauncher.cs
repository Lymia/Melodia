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
using Melodia.Common.InternalApi;

internal struct LoaderOptions {
    public readonly string GameDirectory;
    public readonly string BaseDirectory;
    public readonly string TempDirectory;
    public readonly List<IPlugin> Plugins = new List<IPlugin>();

    internal string[] PluginPath => new string[] {
        AppDomain.CurrentDomain.BaseDirectory,
        Path.Combine(BaseDirectory, "lib/modules_guest"),
        GameDirectory,
    };

    public LoaderOptions(string gameDirectory, string baseDirectory, string tempDirectory)
    {
        GameDirectory = gameDirectory;
        BaseDirectory = baseDirectory;
        TempDirectory = tempDirectory;
    }

    public LoaderOptions AddPlugin(IPlugin plugin) {
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
    private PatchedAssemblyResolver? resolver = null;
    private readonly Assembly myAssembly = Assembly.GetAssembly(typeof(TargetDomainCallback));

    internal void Init(string gameDirectory, string baseDirectory, string tempDirectory, LogDomainSynchronizer logLocal, InternalCommonDataStore? store) {
        this.gameDirectory = gameDirectory;
        Log.Synchronizer.InitializeFromDomain(logLocal);
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;

        InternalCommonInfo.GameDirectory = gameDirectory;
        InternalCommonInfo.BaseDirectory = baseDirectory;
        InternalCommonInfo.TempDirectory = tempDirectory;
        InternalCommonInfo.DataStore = store;
    }
    internal void CommitPatcher(PatchedAssemblyResolver context) {
        this.resolver = context;
    }

    private Assembly? ResolveAssembly(Object sender, ResolveEventArgs ev) {
        Log.Debug($"Handling ResolveAssembly event for {ev.Name}");
        
        if (ev.Name == myAssembly.FullName) {
            Log.Debug(" - Using callback assembly");
            return myAssembly;
        }

        string name = ev.Name.Split(',')[0].Trim();
        return resolver == null ? null : resolver.Value.ResolveAssembly(name);
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
        Log.Info("[ Preparing to start Crystal Project ]");
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

        callback.Init(options.GameDirectory, options.BaseDirectory, options.TempDirectory, Log.Synchronizer, InternalCommonInfo.DataStore);

        Log.Debug(" - Running plugin BeforePatch hook.");
        foreach (var plugin in options.Plugins) plugin.BeforePatchEarly();
        foreach (var plugin in options.Plugins) plugin.BeforePatch();

        Log.Debug(" - Patching assemblies.");
        var patchContext = new PatcherContext(options.PluginPath);

        // Run the hardcoded passes
        var crystalProjectAssembly = patchContext.LoadAssembly("Crystal Project");
        FixedPasses.OpenInternalClasses(crystalProjectAssembly);
        patchContext.MarkAssemblyModified("Crystal Project");

        // Run the actual plugin passes
        foreach (var plugin in options.Plugins) {
            Log.Debug($"   - Running plugin {plugin.GetType().FullName}");
            plugin.Patch(patchContext);
        }

        // We hardcode this here to avoid having to do the mess of passing this information on to the plugins.
        if (!options.CheckAchivementsEnabled()) {
            DisableSteamAchievements(patchContext.LoadAssembly("Crystal Project"));
            patchContext.MarkAssemblyModified("Crystal Project");
        }

        callback.CommitPatcher(patchContext.ToResolver());

        Log.Debug(" - Running plugin AfterPatch hook.");
        foreach (var plugin in options.Plugins) plugin.AfterPatchEarly();
        foreach (var plugin in options.Plugins) plugin.AfterPatch();

        Log.Debug(" - Setting up environment.");
        Environment.CurrentDirectory = options.GameDirectory;

        stopwatch.Stop();
        Log.Debug($"Finished in {stopwatch.ElapsedMilliseconds} ms");
        Log.Debug();

        Log.Info("[ Launching Crystal Project ]");
        var thread = new Thread(() => {
            appDomain.ExecuteAssemblyByName("Crystal Project", args);
            Log.Debug("Crystal Project terminated.");
        });
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        return thread;
    }
}

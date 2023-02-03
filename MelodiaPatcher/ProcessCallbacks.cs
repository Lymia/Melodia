namespace Melodia.Patcher;

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SDL2;
using Steamworks;

internal sealed class TargetDomainCallback : PersistantRemoteObject {
    private string? gameDirectory;
    private readonly Dictionary<string, byte[]> patchedAssemblies = new Dictionary<string, byte[]>();
    private readonly Assembly myAssembly = Assembly.GetAssembly(typeof(TargetDomainCallback));

    internal void Init(string gameDirectory, LogRemoteReceiver logRemote) {
        this.gameDirectory = gameDirectory;
        Log.InitLoggingForChildDomain(logRemote);
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
    }
    internal void AddOverride(string name, byte[] data) {
        patchedAssemblies[name] = data;
    }

    private Assembly? CheckExtension(string name, string extension) {
        var path = Path.Combine(gameDirectory, $"{name}.{extension}");
        if (File.Exists(path)) {
            Log.Debug($" - Found assembly at {path}");
            return Assembly.LoadFrom(path);
        }
        return null;
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

        var dll = CheckExtension(name, "dll");
        if (dll != null) return dll;

        var exe = CheckExtension(name, "exe");
        if (exe != null) return exe;

        return null;
    }
}

public static class Callbacks {
    public static void OnException(Exception e) {
        Log.Error($"{Program.AssemblyNameString} encountered unexpected error.", e);
    }

    private static readonly string APPID_FILE = "steam_appid.txt";

    public static bool RestartAppIfNecessary(AppId_t unOwnAppID) {
        Log.Trace("Intercepting RestartAppIfNecessary.");

        // Since we're relying on Init directly with no Steam restart, check that the appid file is correct.
        var appIdText = $"{(uint) unOwnAppID}";
        if (File.Exists(APPID_FILE) && File.ReadAllText(APPID_FILE).Trim() != appIdText) {
            Log.MsgBox($"{APPID_FILE} does not match expected appid! Terminating process.");
            System.Environment.Exit(1);
        } else if (!File.Exists(APPID_FILE)) {
            Log.MsgBox($"{APPID_FILE} not found. Did you accidentally run MelodiaPatcher.exe directly?");
            System.Environment.Exit(1);
        }

        return false;
    }
}

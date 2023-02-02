using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using SDL2;
using Steamworks;

namespace CrystalLoad;

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
        Log.Error("Crystal Load encountered unexpected error.", e);
    }
    public static bool RestartAppIfNecessary(AppId_t unOwnAppID) {
        Log.Trace("Intercepting RestartAppIfNecessary.");
        File.WriteAllText("steam_appid.txt", $"{(uint) unOwnAppID}");
        return false;
    }
}

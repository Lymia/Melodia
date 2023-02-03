using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace Melodia.Common;

/// <summary>
/// A class that helps manage assemblies that may be patched by a program.
/// </summary>
public sealed class PatcherContext : PersistantRemoteObject {
    private static string? findDllOrExe(string directory, string name) {
        string exePath = Path.Combine(directory, $"{name}.exe");
        if (File.Exists(exePath)) return exePath;

        string dllPath = Path.Combine(directory, $"{name}.dll");
        if (File.Exists(dllPath)) return dllPath;

        return null;
    }

    private static AssemblyDef loadAssembly(string name, string path) {
        var assembly = AssemblyDef.Load(path);
        Trace.Assert(assembly.Name == name, $"{name} does not contain the correct assembly!");
        return assembly;
    }

    private readonly Dictionary<string, AssemblyDef> assemblies = new Dictionary<string, AssemblyDef>();
    private readonly HashSet<string> modifiedAssemblies = new HashSet<string>();
    private bool committed = false;

    private readonly string[] searchPath;

    public PatcherContext(string[] searchPath) {
        this.searchPath = searchPath;
    }

    public string? FindAssemblyPath(string name) {
        foreach (var path in searchPath) {
            string? candidate = findDllOrExe(path, name);
            if (candidate != null) return candidate;
        }
        return null;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public AssemblyDef LoadAssembly(string name) {
        Trace.Assert(!committed, "PatcherContext is already committed!");

        if (assemblies.ContainsKey(name)) return assemblies[name];

        string? path = FindAssemblyPath(name);
        if (path == null) throw new System.Exception($"'{name}' cannot be found in the search path!");
        var assembly = loadAssembly(name, path);
        assemblies[name] = assembly;
        return assembly;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void MarkAssemblyModified(string name) {
        Trace.Assert(!committed, "PatcherContext is already committed!");

        modifiedAssemblies.Add(name);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public Dictionary<string, byte[]> CommitModified() {
        var result = new Dictionary<string, byte[]>();
        foreach (var modified in modifiedAssemblies) {
            if (!assemblies.ContainsKey(modified)) continue;

            var assembly = assemblies[modified];
            var settings = new ModuleWriterOptions(assembly.ManifestModule);
            var patchedData = new MemoryStream();
            assembly.Write(patchedData, settings);
            
            result[assembly.Name] = patchedData.ToArray();
        }
        
        assemblies.Clear();
        modifiedAssemblies.Clear();
        committed = true;

        return result;
    }
}
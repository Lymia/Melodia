using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using dnlib.DotNet;
using dnlib.DotNet.Writer;

namespace Melodia.Common;

[Serializable]
internal sealed class AssemblyResolver {
    private readonly string[] searchPath;

    public AssemblyResolver(string[] searchPath) {
        this.searchPath = searchPath;
    }

    private static string? findDllOrExe(string directory, string name) {
        string exePath = Path.Combine(directory, $"{name}.exe");
        if (File.Exists(exePath)) return exePath;

        string dllPath = Path.Combine(directory, $"{name}.dll");
        if (File.Exists(dllPath)) return dllPath;

        return null;
    }

    public string? FindAssemblyPath(string name) {
        foreach (var path in searchPath) {
            string? candidate = findDllOrExe(path, name);
            if (candidate != null) return candidate;
        }
        return null;
    }
}

/// <summary>
/// A class that helps manage assemblies that may be patched by a program.
/// </summary>
public sealed class PatcherContext {
    private static AssemblyDef loadAssembly(string name, string path) {
        var assembly = AssemblyDef.Load(path);
        Trace.Assert(assembly.Name == name, $"{name} does not contain the correct assembly!");
        return assembly;
    }

    private readonly Dictionary<string, AssemblyDef> assemblies = new Dictionary<string, AssemblyDef>();
    private readonly HashSet<string> modifiedAssemblies = new HashSet<string>();

    private readonly AssemblyResolver resolver;

    public PatcherContext(string[] searchPath) {
        resolver = new AssemblyResolver(searchPath);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public AssemblyDef LoadAssembly(string name) {
        if (assemblies.ContainsKey(name)) return assemblies[name];

        string? path = resolver.FindAssemblyPath(name);
        if (path == null) throw new System.Exception($"'{name}' cannot be found in the search path!");
        var assembly = loadAssembly(name, path);
        assemblies[name] = assembly;
        return assembly;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void MarkAssemblyModified(string name) {
        modifiedAssemblies.Add(name);
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public PatchedAssemblyResolver ToResolver() {
        var overrides = new Dictionary<string, byte[]>();
        foreach (var modified in modifiedAssemblies) {
            if (!assemblies.ContainsKey(modified)) continue;

            var assembly = assemblies[modified];
            var settings = new ModuleWriterOptions(assembly.ManifestModule);
            var patchedData = new MemoryStream();
            assembly.Write(patchedData, settings);
            
            overrides[assembly.Name] = patchedData.ToArray();
        }
        return new PatchedAssemblyResolver(resolver, overrides);
    }
}

[Serializable]
public sealed class PatchedAssemblyResolver {
    private readonly AssemblyResolver resolver;
    private readonly Dictionary<string, byte[]> overrides;

    internal PatchedAssemblyResolver(AssemblyResolver resolver, Dictionary<string, byte[]> overrides)
    {
        this.resolver = resolver;
        this.overrides = overrides;
    }

    public Assembly? ResolveAssembly(string name) {
        if (overrides.ContainsKey(name)) return Assembly.Load(overrides[name]);
        var resolvedPath = resolver.FindAssemblyPath(name);
        if (resolvedPath != null) return Assembly.LoadFile(resolvedPath);
        return null;
    }

    public byte[] GetOverrideData(string name) {
        return overrides[name];
    }
}

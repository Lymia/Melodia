namespace Melodia.Patcher;

using Melodia.Common;
using Melodia.Common.InternalApi;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class Program {
    internal static readonly string VersionString;
    internal static readonly string AssemblyNameString;
    static Program()
    {
        var assembly = typeof(Program).Assembly;
        var ver = new Version(assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), true).OfType<AssemblyFileVersionAttribute>().First().Version);
        VersionString = $"{ver.Major}.{ver.Minor}.{ver.Build}";
        AssemblyNameString = assembly.GetName().Name;
    }    

    private static void MainBody(string[] args) {
        Log.Trace($"Game Directory: {args[0]}");
        Log.Trace($"Base Directory: {args[1]}");
        Log.Trace($"Temp Directory: {args[2]}");
        Log.Trace();

        InternalCommonInfo.GameDirectory = args[0];
        InternalCommonInfo.BaseDirectory = args[1];
        InternalCommonInfo.TempDirectory = args[2];
        InternalCommonInfo.DataStore = new InternalCommonDataStore();
        
        Log.Info("[ Loading MelodiaPatcher plugins ]");
        var options = new LoaderOptions(args[0], args[1], args[2]);
        options.AddPlugin(new BuiltinPlugin());

        foreach (var file in Directory.EnumerateFiles(Path.Combine(CommonInfo.BaseDirectory, "lib/modules_host"))) {
            if (file.EndsWith(".dll")) {
                Log.Debug($" - Loading assembly at '{file}'");

                var assembly = Assembly.LoadFile(file);
                foreach (var type in assembly.ExportedTypes) {
                    if (type.IsInstanceOfType(typeof(IPlugin))) {
                        Log.Debug($"   - Loading plugin {type.FullName}");
                        var plugin = (IPlugin) Activator.CreateInstance(type);
                        options.AddPlugin(plugin);
                    }
                }
            }
        }

        Log.Debug(" - Initializing plugins.");
        foreach (var plugin in options.Plugins) plugin.InitEarly();
        foreach (var plugin in options.Plugins) plugin.Init();

        ProcessLauncher.StartProcess(options, args.Skip(3).ToArray());
    }

    [LoaderOptimization(LoaderOptimization.MultiDomain)]
    internal static void Main(string[] args)
    {
        if (args.Length < 3) {
            Log.MsgBox("Please do not execute this application directly. Run Melodia.exe instead.");
            return;
        }

        Log.InitLogging(AssemblyNameString, args[1]);
        Log.Trace($"{AssemblyNameString} version {VersionString}");
        Log.Trace();

        try {
            MainBody(args);
        } catch (PatcherException e) {
            Log.Error(e.Message);
        } catch (Exception e) {
            Log.Error(null, e);
        }

        if (Log.ErrorLogged) {
            Log.MsgBox("Errors encountered during loading process. A log file will be opened.");

            using (var p = new Process()) {
                p.StartInfo.UseShellExecute = true;
                p.StartInfo.FileName = Log.LogFileLocation;
                p.Start();
            }
        }
    }
}
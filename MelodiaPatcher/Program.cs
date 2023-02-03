namespace Melodia.Patcher;

using Melodia.Common;
using System;
using System.Diagnostics;
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
        if (args.Length < 2) 
            throw new Exception($"Not enough arguments passed to {AssemblyNameString}!");

        Log.Trace($"Game Directory: {args[0]}");
        Log.Trace($"Base Directory: {args[1]}");
        
        var options = new LoaderOptions(args[0], args[1]);
        options.AddPlugin(new BuiltinPlugin());
        ProcessLauncher.StartProcess(options, args.Skip(2).ToArray());
    }

    [LoaderOptimization(LoaderOptimization.MultiDomain)]
    internal static void Main(string[] args)
    {
        Log.InitLogging(AssemblyNameString);
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
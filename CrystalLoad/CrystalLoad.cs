﻿namespace CrystalLoad;

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

internal static class CrystalLoad {
    private static readonly string VersionString;
    static CrystalLoad()
    {
        Version ver = new Version(typeof(CrystalLoad).Assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), true).OfType<AssemblyFileVersionAttribute>().First().Version);
        VersionString = $"{ver.Major}.{ver.Minor}.{ver.Build}";
    }    

    private static void MainBody(string[] args) {
        if (args.Length != 1) 
            throw new Exception("Not enough arguments passed to CrystalLoad Main!");

        Log.Trace($"Target .dll to load: {args[0]}");
        
        ProcessLauncher.StartProcess(new LoaderOptions(AppDomain.CurrentDomain.BaseDirectory), new string[0]);
    }

    [LoaderOptimization(LoaderOptimization.MultiDomain)]
    internal static void Main(string[] args)
    {
        Log.InitLogging();
        Log.Trace($"CrystalLoad version {VersionString}");
        Log.Trace();

        try {
            MainBody(args);
        } catch (CrystalLoadException e) {
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
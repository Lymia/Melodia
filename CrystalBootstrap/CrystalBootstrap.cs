namespace CrystalBootstrap;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Threading;
using SDL2;

internal sealed class CrystalBootstrapException : Exception {
    public CrystalBootstrapException(string message) : base(message) { }
}

internal static class CrystalBootstrap {
    private static readonly string VersionString;
    static CrystalBootstrap()
    {
        Version ver = new Version(typeof(CrystalBootstrap).Assembly.GetCustomAttributes(typeof(AssemblyFileVersionAttribute), true).OfType<AssemblyFileVersionAttribute>().First().Version);
        VersionString = $"{ver.Major}.{ver.Minor}.{ver.Build}";
    }

    private static readonly NamedPermissionSet FULL_TRUST = new NamedPermissionSet("FullTrust");

    private static void MsgBox(string msg) {
        SDL.SDL_ShowSimpleMessageBox(SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR, "CrystalPatcher Bootstrap", msg, IntPtr.Zero);
    }
    
    private static Thread BootstrapProcess(string gameDirectory, string appDirectory, string appName) {
        var setup = new AppDomainSetup
        {
            ApplicationBase = appDirectory,
            ApplicationName = "CrystalPatcher",
            DisallowCodeDownload = true,
            DisallowPublisherPolicy = true
        };
        setup.PrivateBinPath = gameDirectory;

        var appDomain = AppDomain.CreateDomain(appName, null, setup, FULL_TRUST);

        Environment.CurrentDirectory = appDirectory;

        var thread = new Thread(() => {
            appDomain.ExecuteAssemblyByName(appName);
        });
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        return thread;
    }

    private static void MainBody(string[] args) {
        if (args.Length != 2) 
            throw new CrystalBootstrapException("Please do not execute this application directly.");

        var gameDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var appDirectory = args[0];
        var appName = args[1];

        if (!File.Exists(gameDirectory))
            throw new CrystalBootstrapException("Game directory not found or is invalid.");
        if (!File.Exists(appDirectory))
            throw new CrystalBootstrapException("Application directory not found or is invalid.");

        Console.WriteLine($"CrystalBootstrap: Loading assembly '{appName}' from directory '{appDirectory}'");

        BootstrapProcess(gameDirectory, appDirectory, appName);
    }

    [LoaderOptimization(LoaderOptimization.MultiDomain)]
    internal static void Main(string[] args)
    {
        try {
            MainBody(args);
        } catch (CrystalBootstrapException e) {
            MsgBox(e.Message);
        } catch (Exception e) {
            MsgBox($"Error encountered during bootstrap process: {e}");
            throw e;
        }
    }
}
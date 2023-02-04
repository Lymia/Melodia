namespace Melodia.Bootstrap;

using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using SDL2;

internal sealed class BootstrapException : Exception {
    public BootstrapException(string message) : base(message) { }
}

internal static class Program {
    private static readonly NamedPermissionSet FULL_TRUST = new NamedPermissionSet("FullTrust");

    private static void MsgBox(string msg) {
        SDL.SDL_ShowSimpleMessageBox(SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR, "MelodiaBootstrap", msg, IntPtr.Zero);
    }
    
    private static Thread BootstrapProcess(string gameDirectory, string appDirectory, string appName, string[] args) {
        var setup = new AppDomainSetup {
            ApplicationBase = appDirectory,
            ApplicationName = appName,
            DisallowCodeDownload = true,
            DisallowPublisherPolicy = true
        };
        setup.PrivateBinPath = gameDirectory;

        var appDomain = AppDomain.CreateDomain(appName, null, setup, FULL_TRUST);

        Environment.CurrentDirectory = appDirectory;

        var thread = new Thread(() => {
            appDomain.ExecuteAssemblyByName(appName, args);
        });
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        return thread;
    }

    private static void MainBody(string[] args) {
        if (args.Length < 3) 
            throw new BootstrapException("Please do not execute this application directly. Run Melodia.exe instead.");

        var gameDirectory = args[0];
        var appDirectory = args[1];
        var appName = args[2];
        var remainingArgs = args.Skip(3).ToArray();

        Console.WriteLine($"Bootstrap gameDirectory: {gameDirectory}");
        Console.WriteLine($"Bootstrap appDirectory: {appDirectory}");
        Console.WriteLine($"Bootstrap appName: {appName}");
        Console.WriteLine($"Bootstrap remainingArgs.Length: {remainingArgs.Length}");

        if (!Directory.Exists(gameDirectory))
            throw new BootstrapException("Game directory not found or is invalid.");
        if (!Directory.Exists(appDirectory))
            throw new BootstrapException("Application directory not found or is invalid.");

        Console.WriteLine($"MelodiaBootstrap: Loading assembly '{appName}' from directory '{appDirectory}'");

        BootstrapProcess(gameDirectory, appDirectory, appName, remainingArgs);
    }

    [LoaderOptimization(LoaderOptimization.MultiDomain)]
    internal static void Main(string[] args) {
        try {
            MainBody(args);
        } catch (BootstrapException e) {
            MsgBox(e.Message);
        } catch (Exception e) {
            MsgBox($"Error encountered during bootstrap process: {e}");
            throw e;
        }
    }
}

namespace Melodia.Patcher;

using SDL2;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Normally objects with MarshalByRefObject time out after 6 minutes (!!). This prevents that
/// from happening. Since we only ever create a small handful of remote objects, this isn't
/// an issue.
/// </summary>
internal abstract class PersistantRemoteObject : MarshalByRefObject
{
    [SecurityPermissionAttribute(SecurityAction.Demand, Flags = SecurityPermissionFlag.Infrastructure)]
    public override object? InitializeLifetimeService()
    {
        return null;
    }
}

internal static partial class Utils
{
    public static string NormalizeLineEndings(string data) =>
        Regex.Replace(data, @"\r\n?|\n", "\r\n");
}

internal interface LogRemoteReceiver {
    string? LogFileLocation { get; }
    bool ErrorLogged { get; set; }
    bool VerboseMode { get; set; }
    public void WriteLogFile(string taggedMsg);
}

internal class LogRemoteReceiverImpl : PersistantRemoteObject, LogRemoteReceiver
{
    private StreamWriter? outStream;
    public string? LogFileLocation { get { return LogFileLocationInternal; } }
    public bool ErrorLogged { get; set; } = false;
    public bool VerboseMode { get; set; } = false;

    string? LogFileLocationInternal;

    public LogRemoteReceiverImpl(string logFileLocation)
    {
        this.LogFileLocationInternal = logFileLocation;

        var file = File.Open(logFileLocation, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

        try
        {
            this.outStream = new StreamWriter(file);
        }
        catch (Exception e)
        {
            Console.WriteLine(Log.FallbackFormatError("Failed to initialize logging!", e));
            Console.WriteLine();
        }
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void WriteLogFile(string taggedMsg)
    {
        if (outStream == null) return;
        outStream.WriteLine(Utils.NormalizeLineEndings($"[{DateTime.Now}] {taggedMsg}"));
        outStream.Flush();
    }
}

internal class LogRemoteReceiverNull : LogRemoteReceiver {
    public string? LogFileLocation { get; } = null;
    public bool ErrorLogged { get; set; } = false;
    public bool VerboseMode { get; set; } = false;
    public void WriteLogFile(string taggedMsg) {}
}

/// <summary>
/// Exceptions of this type are printed directly to console with no stack trace or message type.
/// </summary>
public sealed class PatcherException : Exception {
    public PatcherException(string message) : base(message) { }
}

public static class Log
{
    internal static LogRemoteReceiver RemoteReceiver { get; private set; } = new LogRemoteReceiverNull();
    private static string LogPrefix = "?";

    internal static string? LogFileLocation { get => RemoteReceiver.LogFileLocation; }
    internal static bool ErrorLogged { get => RemoteReceiver.ErrorLogged; }
    internal static bool VerboseMode { get => RemoteReceiver.VerboseMode;
                                        set => RemoteReceiver.VerboseMode = value; }

    [MethodImpl(MethodImplOptions.Synchronized)]
    internal static void InitLogging()
    {
        RemoteReceiver = new LogRemoteReceiverImpl(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{Program.AssemblyNameString}.log"));
        LogPrefix = " ";
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    internal static void InitLoggingForChildDomain(LogRemoteReceiver remoteReceiver)
    {
        RemoteReceiver = remoteReceiver;
        LogPrefix = "*";
    }
    
    private static string ExceptionMessage(Exception? e, string? message, bool verbose)
    {
        message = message ?? "";
        if (e == null) return message;

        var sb = new StringBuilder();
        sb.Append(message);

        var current = e;
        var hasLastLine = message != "";
        while (current != null)
        {
            if (hasLastLine) sb.Append("\nCaused by: ");

            var excString = current.ToString().Trim();
            if (!verbose && current.Message != null && current.Message != "")
                excString = excString.Split(new char[] { ':' }, 2)[1].Trim();
            sb.Append(excString);

            current = current.InnerException;
        }
        return sb.ToString();
    }
    private static void BaseLog(string tag, bool alwaysTag, bool printConsole, string? message, Exception? e)
    {
        if (printConsole)
            Console.WriteLine(ExceptionMessage(e, $"{(alwaysTag ? $"{tag}: " : "")}{message}", VerboseMode));
        RemoteReceiver.WriteLogFile(ExceptionMessage(e, $"{LogPrefix}{tag.PadRight(5)}: {message}", true));
    }

    internal static string FallbackFormatError(string? msg = null, Exception? e = null) =>
        ExceptionMessage(e, $"Error: {msg}", true);

    public static void Trace(string? msg = null, Exception? e = null) =>
        BaseLog("Trace", false, false, msg, e);
    public static void Debug(string? msg = null, Exception? e = null) =>
        BaseLog("Debug", false, VerboseMode, msg, e);
    public static void Info(string? msg = null, Exception? e = null) =>
        BaseLog("Info", false, true, msg, e);
    public static void Warn(string? msg = null, Exception? e = null) =>
        BaseLog("Warn", false, true, msg, e);
    public static void Error(string? msg = null, Exception? e = null)
    {
        RemoteReceiver.ErrorLogged = true;
        BaseLog("Error", true, true, msg, e);
    }

    internal static void MsgBox(string msg) {
        SDL.SDL_ShowSimpleMessageBox(SDL.SDL_MessageBoxFlags.SDL_MESSAGEBOX_ERROR, Program.AssemblyNameString, msg, IntPtr.Zero);
    }
}

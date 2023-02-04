namespace Melodia.Common;

using System;
using System.Collections.Generic;
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
public abstract class PersistantRemoteObject : MarshalByRefObject
{
    [SecurityPermissionAttribute(SecurityAction.Demand, Flags = SecurityPermissionFlag.Infrastructure)]
    public override object? InitializeLifetimeService()
    {
        return null;
    }
}

internal interface LogRemoteReceiver {
    string? LogFileLocation { get; }
    bool ErrorLogged { get; set; }
    public void WriteLogFile(string taggedMsg);
}

internal class LogRemoteReceiverImpl : PersistantRemoteObject, LogRemoteReceiver
{
    private StreamWriter? outStream;
    public string? LogFileLocation { get => LogFileLocationInternal; }
    public bool ErrorLogged { get; set; } = false;

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

    public static string NormalizeLineEndings(string data) =>
        Regex.Replace(data, @"\r\n?|\n", "\r\n");

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void WriteLogFile(string taggedMsg)
    {
        if (outStream == null) return;
        outStream.WriteLine(NormalizeLineEndings($"[{DateTime.Now}] {taggedMsg}"));
        outStream.Flush();
    }
}

internal class LogRemoteReceiverNull : LogRemoteReceiver {
    public string? LogFileLocation { get; } = null;
    public bool ErrorLogged { get; set; } = false;
    public void WriteLogFile(string taggedMsg) {}
}

/// <summary>
/// Exceptions of this type are printed directly to console with no stack trace or message type.
/// </summary>
public sealed class PatcherException : Exception {
    public PatcherException(string message) : base(message) { }
}

/// <summary>
/// A class that allows for synchromizing logging between two domains.
/// </summary>
public sealed class LogDomainSynchronizer : PersistantRemoteObject {
    internal LogDomainSynchronizer() {}

    internal LogRemoteReceiver getReceiver() {
        return Log.RemoteReceiver;
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    public void InitializeFromDomain(LogDomainSynchronizer parent) {
        Log.InitLoggingForChildDomain(parent.getReceiver());
    }
}

/// <summary>
/// The main class that handles logging in Melodia.
/// </summary>
public static class Log
{
    internal static Boolean initalized = false;
    internal static LogRemoteReceiver RemoteReceiver { get; private set; } = new LogRemoteReceiverNull();
    private static string LogPrefix = "?";

    public static readonly LogDomainSynchronizer Synchronizer = new LogDomainSynchronizer();

    public static string? LogFileLocation { get => RemoteReceiver.LogFileLocation; }
    public static bool ErrorLogged { get => RemoteReceiver.ErrorLogged; }

    /// <summary>
    /// Initializes logging to a given path.
    /// </summary>
    [MethodImpl(MethodImplOptions.Synchronized)]
    public static void InitLogging(string programName, string? baseDirectory = null)
    {
        if (initalized) throw new Exception("Logging is already initialized.");
        initalized = true;
        RemoteReceiver = new LogRemoteReceiverImpl(Path.Combine(baseDirectory ?? AppDomain.CurrentDomain.BaseDirectory, $"{programName}.log"));
        LogPrefix = " ";
    }

    [MethodImpl(MethodImplOptions.Synchronized)]
    internal static void InitLoggingForChildDomain(LogRemoteReceiver remoteReceiver)
    {
        if (initalized) throw new Exception("Logging is already initialized.");
        initalized = true;
        RemoteReceiver = remoteReceiver;
        LogPrefix = "*";
    }
    
    private static string ExceptionMessage(Exception? e, string? message)
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
            sb.Append(excString);

            current = current.InnerException;
        }
        return sb.ToString();
    }
    private static void BaseLog(string tag, bool alwaysTag, bool printConsole, string? message, Exception? e)
    {
        if (printConsole)
            Console.WriteLine(ExceptionMessage(e, $"{(alwaysTag ? $"{tag}: " : "")}{message}"));
        RemoteReceiver.WriteLogFile(ExceptionMessage(e, $"{LogPrefix}{tag.PadRight(5)}: {message}"));
    }

    internal static string FallbackFormatError(string? msg = null, Exception? e = null) =>
        ExceptionMessage(e, $"Error: {msg}");

    public static void Trace(string? msg = null, Exception? e = null) =>
        BaseLog("Trace", false, false, msg, e);
    public static void Debug(string? msg = null, Exception? e = null) =>
        BaseLog("Debug", false, false, msg, e);
    public static void Info(string? msg = null, Exception? e = null) =>
        BaseLog("Info", false, true, msg, e);
    public static void Warn(string? msg = null, Exception? e = null) =>
        BaseLog("Warn", false, true, msg, e);
    public static void Error(string? msg = null, Exception? e = null)
    {
        RemoteReceiver.ErrorLogged = true;
        BaseLog("Error", true, true, msg, e);
    }
}

public static class IListExtension {
    public static void AddRange<T>(this IList<T> list, IEnumerable<T> items) {
        if (list == null) throw new ArgumentNullException(nameof(list));
        if (items == null) throw new ArgumentNullException(nameof(items));

        if (list is List<T> asList) {
            asList.AddRange(items);
        } else {
            foreach (var item in items) list.Add(item);
        }
    }

    public static void InsertRange<T>(this IList<T> list, int idx, IEnumerable<T> items) {
        if (list == null) throw new ArgumentNullException(nameof(list));
        if (items == null) throw new ArgumentNullException(nameof(items));

        if (list is List<T> asList) {
            asList.InsertRange(idx, items);
        } else {
            foreach (var item in items) {
                list.Insert(idx, item);
                idx += 1;
            }
        }
    }

    public static void RemoveAfter<T>(this IList<T> list, int idx) {
        var count = list.Count;
        while (count > idx) {
            count -= 1;
            list.RemoveAt(count);
        }
    }
}
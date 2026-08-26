// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;

namespace ShadowsOfDoubtHeadTracking.Core;

/// <summary>
/// Mirrors this plugin's log lines into HeadTracking.log next to the game
/// executable, where a user can find them without knowing where BepInEx keeps
/// its own log. The file is truncated on every launch, so what it holds is
/// always the current session and it never grows without bound.
/// </summary>
internal sealed class LogFile : ILogListener
{
    internal const string FileName = "HeadTracking.log";

    private readonly string _sourceName;
    private readonly StreamWriter _writer;

    // BepInEx dispatches to listeners on whichever thread logged, and this
    // plugin logs from more than one: OpenTrackReceiver's Log callback fires
    // from its UDP receive thread and its port-retry thread, alongside every
    // main-thread line. StreamWriter is not thread-safe - concurrent writes
    // corrupt its char buffer and throw out of a background thread, which on
    // the receive thread means tracking dies with no line explaining it. The
    // same lock closes the shutdown race, where an in-flight write lands on a
    // writer Dispose() has already closed.
    private readonly object _writeLock = new();
    private bool _disposed;

    public LogLevel LogLevelFilter => LogLevel.All;

    private LogFile(string sourceName, StreamWriter writer)
    {
        _sourceName = sourceName;
        _writer = writer;
    }

    /// <summary>
    /// Opens the log for this session and starts mirroring the named log source
    /// into it. Call before the first line is logged.
    /// </summary>
    internal static LogFile Attach(string sourceName, string header)
    {
        string path = Path.Combine(Paths.GameRootPath, FileName);

        // FileShare.ReadWrite so tailing the file from another process does not
        // stop the next launch from opening it.
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        var writer = new StreamWriter(stream) { AutoFlush = true };
        writer.WriteLine($"{header} - session started {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        var listener = new LogFile(sourceName, writer);
        Logger.Listeners.Add(listener);
        return listener;
    }

    /// <summary>
    /// Writes over a caller-supplied writer, for tests that must not touch the
    /// game directory or BepInEx's listener collection.
    /// </summary>
    internal static LogFile ForWriter(string sourceName, StreamWriter writer)
    {
        return new LogFile(sourceName, writer);
    }

    public void LogEvent(object sender, LogEventArgs eventArgs)
    {
        if (eventArgs.Source.SourceName != _sourceName) return;

        string line = $"[{DateTime.Now:HH:mm:ss}] [{eventArgs.Level}] {eventArgs.Data}";
        lock (_writeLock)
        {
            if (_disposed) return;
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        Logger.Listeners.Remove(this);

        lock (_writeLock)
        {
            if (_disposed) return;
            _disposed = true;
            _writer.Dispose();
        }
    }
}

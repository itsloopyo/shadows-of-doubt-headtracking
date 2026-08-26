// SPDX-License-Identifier: MIT
// Copyright (c) 2026 itsloopyo

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using ShadowsOfDoubtHeadTracking.Core;
using Xunit;

namespace ShadowsOfDoubtHeadTracking.Tests;

/// <summary>
/// BepInEx dispatches a log event to every listener synchronously, on whichever
/// thread logged it (Logger.LogListenerCollection.SendLogEvent). This plugin logs
/// from more than one thread: OpenTrackReceiver's Log callback fires from its UDP
/// receive thread and its port-retry thread, alongside every main-thread line. So
/// LogFile.LogEvent is a concurrent entry point and has to behave like one.
/// </summary>
public class LogFileTests
{
    private const string SourceName = "Head Tracking";

    private static LogEventArgs Event(string source, string data)
    {
        return new LogEventArgs(data, LogLevel.Info, new ManualLogSource(source));
    }

    private static (LogFile log, MemoryStream sink) NewLog()
    {
        var sink = new MemoryStream();
        var writer = new StreamWriter(sink, new UTF8Encoding(false)) { AutoFlush = true };
        return (LogFile.ForWriter(SourceName, writer), sink);
    }

    private static List<string> Lines(MemoryStream sink)
    {
        return new UTF8Encoding(false)
            .GetString(sink.ToArray())
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    [Fact]
    public void ConcurrentLogEvents_KeepEveryLineWholeAndAccountedFor()
    {
        const int threads = 8;
        const int perThread = 250;

        var (log, sink) = NewLog();
        using var start = new Barrier(threads);

        Parallel.For(0, threads, t =>
        {
            start.SignalAndWait();
            for (int i = 0; i < perThread; i++)
            {
                log.LogEvent(this, Event(SourceName, $"payload-{t}-{i}"));
            }
        });

        log.Dispose();

        var lines = Lines(sink);
        Assert.Equal(threads * perThread, lines.Count);

        // An unsynchronised StreamWriter does not merely interleave - it corrupts its
        // own char buffer, so lines come back spliced, truncated or duplicated. Both
        // assertions have to hold: every payload present exactly once, and no line
        // carrying anything but its own payload.
        var expected = new HashSet<string>(
            from t in Enumerable.Range(0, threads)
            from i in Enumerable.Range(0, perThread)
            select $"payload-{t}-{i}");

        foreach (string line in lines)
        {
            int marker = line.IndexOf("payload-", StringComparison.Ordinal);
            Assert.True(marker >= 0, $"line carries no payload: '{line}'");

            string payload = line.Substring(marker);
            Assert.True(expected.Remove(payload), $"payload missing, spliced or duplicated: '{line}'");
        }

        Assert.Empty(expected);
    }

    [Fact]
    public void LogEventAfterDispose_IsDroppedRatherThanThrowing()
    {
        var (log, sink) = NewLog();
        log.LogEvent(this, Event(SourceName, "before"));
        log.Dispose();

        // The receive thread can be mid-dispatch while Unload() disposes the log.
        // Writing to the closed writer would throw ObjectDisposedException out of a
        // background thread, which is not a recoverable place to raise it.
        log.LogEvent(this, Event(SourceName, "after"));

        var lines = Lines(sink);
        Assert.Single(lines);
        Assert.Contains("before", lines[0]);
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        var (log, _) = NewLog();
        log.Dispose();
        log.Dispose();
    }

    [Fact]
    public void LinesFromAnotherLogSource_AreNotMirrored()
    {
        var (log, sink) = NewLog();

        log.LogEvent(this, Event("SomeOtherPlugin", "not ours"));
        log.LogEvent(this, Event(SourceName, "ours"));
        log.Dispose();

        var lines = Lines(sink);
        Assert.Single(lines);
        Assert.Contains("ours", lines[0]);
        Assert.DoesNotContain("not ours", lines[0]);
    }
}

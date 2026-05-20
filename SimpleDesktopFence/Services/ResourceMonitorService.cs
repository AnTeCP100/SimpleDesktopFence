using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Timers;

namespace SimpleDesktopFence.Services
{
    public record ResourceSample(DateTime Time, double CpuPercent, double RamMb);

    public static class ResourceMonitorService
    {
        private static readonly Process _proc = Process.GetCurrentProcess();
        private static readonly System.Timers.Timer _timer = new(5_000); // 5-second interval

        private static TimeSpan _prevCpuTime = TimeSpan.Zero;
        private static DateTime _prevSampleAt = DateTime.Now;

        // In-memory store; cleared automatically when the process exits
        public static readonly List<ResourceSample> Samples = new();

        // Fired on the timer thread; subscribers must marshal to UI thread if needed
        public static event Action<ResourceSample>? SampleAdded;

        public static DateTime SessionStart { get; } = DateTime.Now;

        // Latest sample, or null if none yet
        public static ResourceSample? Latest =>
            Samples.Count > 0 ? Samples[^1] : null;

        static ResourceMonitorService()
        {
            _timer.Elapsed += OnElapsed;
            _timer.AutoReset = true;
        }

        public static void Start()
        {
            _prevCpuTime = _proc.TotalProcessorTime;
            _prevSampleAt = DateTime.Now;
            _timer.Start();
        }

        public static void Stop() => _timer.Stop();

        // ── Private ───────────────────────────────────────────────────────────

        private static void OnElapsed(object? sender, ElapsedEventArgs e)
        {
            _proc.Refresh();

            var now = DateTime.Now;
            var cpuUsed = _proc.TotalProcessorTime - _prevCpuTime;
            var elapsed = now - _prevSampleAt;
            double cpuPct = elapsed.TotalMilliseconds > 0
                ? cpuUsed.TotalMilliseconds / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100
                : 0;

            _prevCpuTime = _proc.TotalProcessorTime;
            _prevSampleAt = now;

            double ramMb = _proc.PrivateMemorySize64 / (1024.0 * 1024);

            var sample = new ResourceSample(now,
                Math.Round(Math.Max(0, cpuPct), 1),
                Math.Round(ramMb, 1));

            Samples.Add(sample);
            SampleAdded?.Invoke(sample);
        }
    }
}

using System.Diagnostics;

namespace CmdPalDockPlus.Providers;

public sealed record ProcessMetricSnapshot(double? CpuPercent, long MemoryBytes, TimeSpan Uptime);
public interface IProcessMetricsReader { ProcessMetricSnapshot Read(int processId); }

public sealed class ProcessMetricsReader : IProcessMetricsReader
{
    private readonly object _gate = new();
    private readonly Dictionary<int, (TimeSpan Cpu, DateTimeOffset At)> _last = [];
    public ProcessMetricSnapshot Read(int processId)
    {
        using var process = Process.GetProcessById(processId);
        var now = DateTimeOffset.UtcNow;
        var cpu = process.TotalProcessorTime;
        double? percent = null;
        lock (_gate)
        {
            if (_last.TryGetValue(processId, out var previous))
            {
                var wall = (now - previous.At).TotalMilliseconds;
                var used = (cpu - previous.Cpu).TotalMilliseconds;
                if (wall > 0) percent = Math.Max(0, used / wall / Math.Max(1, Environment.ProcessorCount) * 100.0);
            }
            _last[processId] = (cpu, now);
        }
        return new ProcessMetricSnapshot(percent, process.WorkingSet64, now - process.StartTime.ToUniversalTime());
    }
}

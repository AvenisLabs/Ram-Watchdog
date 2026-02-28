// MemoryMonitor.cs — Process memory enumeration and threshold logic v1.3.0
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RamWatchdog;

/// <summary>
/// Snapshot of a single process group's memory usage.
/// </summary>
public sealed record ProcessMemoryInfo(
    string Name,
    long MemoryBytes,
    int ProcessCount,
    bool ExceedsThreshold)
{
    public double MemoryMB => MemoryBytes / (1024.0 * 1024.0);
    public double MemoryGB => MemoryBytes / (1024.0 * 1024.0 * 1024.0);
}

/// <summary>
/// Polls running processes on a timer and fires an event with memory snapshots.
/// Threshold is configurable; defaults to 16 GB or 30% of physical RAM (whichever is lower).
/// </summary>
public sealed class MemoryMonitor : IDisposable
{
    private const long OneGB = 1024L * 1024 * 1024;
    private const long DefaultHardThresholdBytes = 16L * OneGB;
    private const double DefaultRamPercent = 0.30;
    private const long DefaultDisplayFloorBytes = 500L * 1024 * 1024; // default: show processes >= 500 MB

    private readonly System.Threading.Timer _timer;
    private readonly long _totalPhysicalRam;
    private long _thresholdBytes;
    private long _displayFloorBytes = DefaultDisplayFloorBytes;
    private bool _disposed;

    /// <summary>Fires each poll cycle with the current list of high-memory processes.</summary>
    public event Action<List<ProcessMemoryInfo>>? OnSnapshot;

    public const double OneGBd = 1024.0 * 1024.0 * 1024.0;

    public long ThresholdBytes => _thresholdBytes;
    public long TotalPhysicalRam => _totalPhysicalRam;

    /// <summary>Computes the automatic default threshold for this system.</summary>
    public long DefaultThresholdBytes { get; }

    // ── GB convenience properties (eliminates repeated byte-to-GB divisions) ──
    public double ThresholdGB => _thresholdBytes / OneGBd;
    public double TotalPhysicalRamGB => _totalPhysicalRam / OneGBd;
    public double DefaultThresholdGB => DefaultThresholdBytes / OneGBd;
    public double DisplayFloorGB => _displayFloorBytes / OneGBd;
    public double DisplayFloorMB => _displayFloorBytes / (1024.0 * 1024.0);

    public MemoryMonitor()
    {
        _totalPhysicalRam = GetTotalPhysicalMemory();

        // Automatic default: 16 GB or 30% of RAM, whichever is lower
        long percentThreshold = (long)(_totalPhysicalRam * DefaultRamPercent);
        DefaultThresholdBytes = Math.Min(DefaultHardThresholdBytes, percentThreshold);
        _thresholdBytes = DefaultThresholdBytes;

        // Timer fires every 2 seconds; initial delay of 500ms to let the UI initialize
        _timer = new System.Threading.Timer(Poll, null, 500, 2000);
    }

    /// <summary>Updates the alert threshold at runtime (in bytes).</summary>
    public void SetThreshold(long bytes)
    {
        _thresholdBytes = bytes;
    }

    /// <summary>Updates the display floor at runtime (in bytes). Processes below this are hidden.</summary>
    public void SetDisplayFloor(long bytes)
    {
        _displayFloorBytes = bytes > 0 ? bytes : DefaultDisplayFloorBytes;
    }

    public long DisplayFloorBytes => _displayFloorBytes;

    private void Poll(object? state)
    {
        try
        {
            var snapshot = BuildSnapshot();
            OnSnapshot?.Invoke(snapshot);
        }
        catch
        {
            // Swallow exceptions from process enumeration (processes can exit mid-read)
        }
    }

    /// <summary>
    /// Enumerates all processes, groups by name, sums WorkingSet64,
    /// and returns those above the configurable display floor.
    /// </summary>
    private List<ProcessMemoryInfo> BuildSnapshot()
    {
        var processes = Process.GetProcesses();
        var grouped = new Dictionary<string, (long bytes, int count)>(StringComparer.OrdinalIgnoreCase);

        foreach (var proc in processes)
        {
            try
            {
                string name = proc.ProcessName;
                long ws = proc.WorkingSet64;

                if (grouped.TryGetValue(name, out var existing))
                    grouped[name] = (existing.bytes + ws, existing.count + 1);
                else
                    grouped[name] = (ws, 1);
            }
            catch
            {
                // Process exited between enumeration and property read — skip it
            }
            finally
            {
                proc.Dispose();
            }
        }

        var results = new List<ProcessMemoryInfo>();
        foreach (var (name, (bytes, count)) in grouped)
        {
            if (bytes >= _displayFloorBytes)
            {
                results.Add(new ProcessMemoryInfo(name, bytes, count, bytes >= _thresholdBytes));
            }
        }

        // Sort descending by memory
        results.Sort((a, b) => b.MemoryBytes.CompareTo(a.MemoryBytes));
        return results;
    }

    /// <summary>
    /// Queries total physical RAM via Win32 GlobalMemoryStatusEx.
    /// </summary>
    private static long GetTotalPhysicalMemory()
    {
        var memInfo = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (GlobalMemoryStatusEx(ref memInfo))
            return (long)memInfo.ullTotalPhys;

        // Fallback: use GC info (less accurate but functional)
        return GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _timer.Dispose();
        }
    }
}

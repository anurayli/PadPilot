using PadPilot.Core.Hid;

namespace PadPilot.Core;

/// <summary>Pad'leri otomatik bulur, takilip cikarilmalarini izler.</summary>
public sealed class PadManager : IDisposable
{
    readonly Dictionary<string, PadDevice> _pads = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _ignored = new(StringComparer.OrdinalIgnoreCase);
    readonly object _lock = new();
    Timer? _timer;
    bool _disposed;

    public event Action<PadDevice>? PadAdded;
    public event Action<PadDevice>? PadRemoved;

    public void Start() => _timer = new Timer(_ => Scan(), null, 0, 1500);

    void Scan()
    {
        if (!Monitor.TryEnter(_lock)) return;
        try
        {
            if (_disposed) return;
            var paths = HidNative.EnumerateHidPaths();
            _ignored.IntersectWith(paths);
            foreach (string path in paths)
            {
                if (_pads.ContainsKey(path) || _ignored.Contains(path)) continue;
                var pad = PadDevice.TryOpen(path, out bool notOurs);
                if (pad == null) { if (notOurs) _ignored.Add(path); continue; }
                _pads[path] = pad;
                pad.Disconnected += Remove;
                pad.Start();
                PadAdded?.Invoke(pad);
            }
        }
        finally { Monitor.Exit(_lock); }
    }

    void Remove(PadDevice pad)
    {
        lock (_lock) { if (!_pads.Remove(pad.Path)) return; }
        PadRemoved?.Invoke(pad);
        pad.Dispose();
    }

    public void Dispose()
    {
        _timer?.Dispose();
        PadDevice[] pads;
        lock (_lock) { _disposed = true; pads = [.. _pads.Values]; _pads.Clear(); }
        foreach (var pad in pads) pad.Dispose();
    }
}

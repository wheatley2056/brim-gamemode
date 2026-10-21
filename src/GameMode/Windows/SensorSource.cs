using System.Runtime.InteropServices;

namespace Brim.GameMode.Windows;

/// <summary>
/// CPU, GPU und Grafikspeicher über Leistungsindikatoren (PDH, wie der Task-Manager), die GPU-Temperatur vom
/// Grafiktreiber (<c>D3DKMTQueryAdapterInfo</c>, wie der Task-Manager). Alles ohne Adminrechte.
/// </summary>
internal sealed partial class SensorSource : ISensorSource
{
    private readonly Pdh.Query _query = new();
    private readonly Pdh.Counter? _cpu;
    private readonly Pdh.Counter? _engines;
    private readonly Pdh.Counter? _processMemory;
    private readonly Dictionary<(int High, uint Low), uint> _adapters = [];
    private bool _adaptersRead;

    public SensorSource()
    {
        // „Processor Utility“ entspricht dem Task-Manager seit Windows 8 (berücksichtigt den Turbotakt).
        _cpu = _query.TryAdd(@"\Processor Information(_Total)\% Processor Utility")
            ?? _query.TryAdd(@"\Processor(_Total)\% Processor Time");
        _engines = _query.TryAdd(@"\GPU Engine(*)\Utilization Percentage");
        _processMemory = _query.TryAdd(@"\GPU Process Memory(*)\Dedicated Usage");
        _query.Collect();
    }

    public SystemSample Sample(int gamePid)
    {
        _query.Collect();
        var cpu = _cpu?.Value() is { } c ? Math.Clamp(c, 0, 100) : (double?)null;
        var busiest = _engines is null ? null : SensorMath.BusiestAdapter(_engines.Values());
        var vram = gamePid != 0 && _processMemory is not null ? SensorMath.ProcessVram(_processMemory.Values(), gamePid) : null;
        double? temperature = null;
        if (busiest is { } gpu && SensorMath.ParseLuid(gpu.Luid) is { } luid)
        {
            temperature = Temperature(luid);
        }
        return new SystemSample(cpu, busiest?.Utilization, temperature, vram);
    }

    // ───────── GPU-Temperatur (D3DKMT) ─────────

    private const int AdapterPerfData = 62; // KMTQAITYPE_ADAPTERPERFDATA, ab WDDM 2.4

    [StructLayout(LayoutKind.Sequential)]
    private struct AdapterInfo
    {
        public uint Handle;
        public uint LuidLow;
        public int LuidHigh;
        public uint NumOfSources;
        public int PrecisePresentRegionsPreferred;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct EnumAdapters2
    {
        public uint NumAdapters;
        public AdapterInfo* Adapters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct QueryAdapterInfo
    {
        public uint Adapter;
        public int Type;
        public void* PrivateDriverData;
        public uint PrivateDriverDataSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AdapterPerfDataStruct
    {
        public uint PhysicalAdapterIndex;
        public ulong MemoryFrequency;
        public ulong MaxMemoryFrequency;
        public ulong MaxMemoryFrequencyOC;
        public ulong MemoryBandwidth;
        public ulong PCIEBandwidth;
        public uint FanRPM;
        public uint Power;
        public uint Temperature;
        public byte PowerStateOverride;
    }

    [LibraryImport("gdi32.dll")]
    private static partial int D3DKMTEnumAdapters2(ref EnumAdapters2 adapters);

    [LibraryImport("gdi32.dll")]
    private static partial int D3DKMTQueryAdapterInfo(ref QueryAdapterInfo info);

    [LibraryImport("gdi32.dll")]
    private static partial int D3DKMTCloseAdapter(ref uint adapter);

    private unsafe double? Temperature((int High, uint Low) luid)
    {
        if (!_adapters.ContainsKey(luid))
        {
            if (_adaptersRead)
            {
                // Neuer Adapter (z. B. eGPU) – einmal neu einlesen.
                CloseAdapters();
            }
            ReadAdapters();
        }
        if (!_adapters.TryGetValue(luid, out var handle))
        {
            return null;
        }
        var perf = new AdapterPerfDataStruct();
        var query = new QueryAdapterInfo
        {
            Adapter = handle,
            Type = AdapterPerfData,
            PrivateDriverData = &perf,
            PrivateDriverDataSize = (uint)sizeof(AdapterPerfDataStruct),
        };
        return D3DKMTQueryAdapterInfo(ref query) == 0 ? SensorMath.Temperature(perf.Temperature) : null;
    }

    private unsafe void ReadAdapters()
    {
        _adaptersRead = true;
        var request = new EnumAdapters2();
        if (D3DKMTEnumAdapters2(ref request) != 0 || request.NumAdapters == 0)
        {
            return;
        }
        var buffer = new AdapterInfo[request.NumAdapters];
        fixed (AdapterInfo* adapters = buffer)
        {
            request.Adapters = adapters;
            if (D3DKMTEnumAdapters2(ref request) != 0)
            {
                return;
            }
        }
        for (var i = 0; i < request.NumAdapters; i++)
        {
            _adapters[(buffer[i].LuidHigh, buffer[i].LuidLow)] = buffer[i].Handle;
        }
    }

    private void CloseAdapters()
    {
        foreach (var handle in _adapters.Values)
        {
            var h = handle;
            _ = D3DKMTCloseAdapter(ref h);
        }
        _adapters.Clear();
    }

    public void Dispose()
    {
        CloseAdapters();
        _query.Dispose();
    }
}

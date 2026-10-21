using System.Runtime.InteropServices;
using Brim.Core;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;

namespace Brim.GameMode.Windows;

/// <summary>
/// Bildrate wie PresentMon: eine ETW-Echtzeitsitzung auf DXGI und D3D9, gezählt werden nur die Presents des
/// beobachteten Prozesses. Braucht Adminrechte oder die Gruppe „Leistungsprotokollbenutzer“.
/// </summary>
internal sealed class EtwFrameSource : IFrameSource
{
    /// <summary>Eigener Name, damit sie neben der eingebauten Messung von Brim („Brim-Frames“) laufen kann.</summary>
    private const string SessionName = "Brim-GameMode";

    /// <summary>Microsoft-Windows-DXGI: Present_Start (42), PresentMultiplaneOverlay_Start (55).</summary>
    private static readonly Guid Dxgi = new("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");

    /// <summary>Microsoft-Windows-D3D9: Present_Start (1), für ältere Spiele.</summary>
    private static readonly Guid D3D9 = new("783ACA0A-790E-4D7F-8451-AA850511C6B9");

    private readonly Lock _gate = new();
    private readonly FrameCounter _counter = new();
    private TraceEventSession? _session;
    private int _watched;
    private DateTime _lastEvent = DateTime.MinValue;
    private double _lastTimestamp;

    public FrameSourceState State { get; private set; } = FrameSourceState.Starting;
    public string? Message { get; private set; }

    public void Start()
    {
        try
        {
            // Eine Sitzung von einem abgestürzten Vorgänger würde den Namen blockieren.
            TraceEventSession.GetActiveSession(SessionName)?.Stop(noThrow: true);
            _session = new TraceEventSession(SessionName) { StopOnDispose = true };
            _session.EnableProvider(Dxgi, TraceEventLevel.Informational);
            _session.EnableProvider(D3D9, TraceEventLevel.Informational);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || (ex is COMException com && com.HResult == unchecked((int)0x80070005)))
        {
            Stop();
            State = FrameSourceState.Denied;
            Message = ex.Message;
            return;
        }
        catch (Exception ex)
        {
            Stop();
            State = FrameSourceState.Failed;
            Message = ex.Message;
            return;
        }

        _session.Source.AllEvents += OnEvent;
        var session = _session;
        _ = Task.Run(() =>
        {
            try
            {
                session.Source.Process();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"ETW-Sitzung beendet: {ex.Message}");
                State = FrameSourceState.Failed;
                Message = ex.Message;
            }
        });
        State = FrameSourceState.Running;
    }

    private void OnEvent(TraceEvent e)
    {
        var isPresent = (e.ProviderGuid == Dxgi && (int)e.ID is 42 or 55) || (e.ProviderGuid == D3D9 && (int)e.ID == 1);
        if (!isPresent)
        {
            return;
        }
        lock (_gate)
        {
            if (e.ProcessID != _watched)
            {
                return;
            }
            _counter.Add(e.TimeStampRelativeMSec);
            _lastTimestamp = e.TimeStampRelativeMSec;
            _lastEvent = DateTime.UtcNow;
        }
    }

    public void Watch(int pid)
    {
        lock (_gate)
        {
            if (_watched != pid)
            {
                _watched = pid;
                _counter.Clear();
                _lastEvent = DateTime.MinValue;
            }
        }
    }

    public FrameStats? Read()
    {
        lock (_gate)
        {
            // Echtzeit-ETW liefert gepuffert, etwa einmal pro Sekunde. Deshalb zählt die Zeit des letzten
            // Ereignisses; kam zwei Sekunden nichts, gibt der Prozess keine Bilder mehr aus.
            return DateTime.UtcNow - _lastEvent > TimeSpan.FromSeconds(2) ? null : _counter.Snapshot(_lastTimestamp);
        }
    }

    private void Stop()
    {
        try
        {
            _session?.Stop(noThrow: true);
        }
        finally
        {
            _session?.Dispose();
            _session = null;
        }
    }

    public void Dispose() => Stop();
}

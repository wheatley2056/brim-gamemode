using Brim.Core;

namespace Brim.GameMode;

// Was das Plugin von Windows braucht, als Schnittstellen: Die Tests setzen Fakes ein, Program.cs die echten Quellen.

/// <summary>Das Fenster im Vordergrund.</summary>
/// <param name="ProcessName">Prozessname ohne <c>.exe</c>, z. B. <c>eldenring</c>.</param>
/// <param name="DisplayName">Anzeigename (Dateibeschreibung der Exe), sonst der Prozessname.</param>
/// <param name="ExePath">Voller Pfad der Exe; <c>null</c>, wenn Windows ihn nicht verrät.</param>
/// <param name="CoversMonitor">Füllt das Fenster seinen Monitor ganz (Vollbild oder randlos)?</param>
public sealed record ForegroundWindow(int Pid, string ProcessName, string DisplayName, string? ExePath, bool CoversMonitor);

public interface IForegroundSource
{
    ForegroundWindow? Read();
}

public enum FrameSourceState
{
    Starting,
    Running,
    /// <summary>Keine Rechte für ETW: Gruppe „Leistungsprotokollbenutzer“ fehlt.</summary>
    Denied,
    Failed,
}

/// <summary>Zustand der FPS-Messung für die Anzeige; <paramref name="SetupNote"/> nach einem Klick auf „Einrichten“.</summary>
public sealed record Measurement(FrameSourceState State, string? Message, string? SetupNote);

/// <summary>Bildrate eines Prozesses aus seinen Presents (ETW, wie PresentMon).</summary>
public interface IFrameSource : IDisposable
{
    FrameSourceState State { get; }

    /// <summary>Warum die Messung nicht läuft (bei <see cref="FrameSourceState.Failed"/>).</summary>
    string? Message { get; }

    /// <summary>Nur noch diesen Prozess zählen (0 = keinen).</summary>
    void Watch(int pid);

    /// <summary>Werte des beobachteten Prozesses; <c>null</c>, solange er keine Bilder ausgibt.</summary>
    FrameStats? Read();
}

/// <summary>Eine Messung des Systems. <c>null</c> = auf diesem Rechner nicht messbar.</summary>
/// <param name="Cpu">CPU-Auslastung in Prozent, wie im Task-Manager.</param>
/// <param name="Gpu">Auslastung der GPU, die am meisten zu tun hat, in Prozent.</param>
/// <param name="GpuTemperature">Temperatur dieser GPU in °C (vom Treiber, wie im Task-Manager).</param>
/// <param name="GameVram">Dedizierter Grafikspeicher, den das Spiel belegt, in Bytes.</param>
public sealed record SystemSample(double? Cpu, double? Gpu, double? GpuTemperature, ulong? GameVram)
{
    public static SystemSample Empty { get; } = new(null, null, null, null);
}

public interface ISensorSource : IDisposable
{
    /// <summary>Misst seit dem letzten Aufruf; <paramref name="gamePid"/> für die Werte des Spiels (0 = keins).</summary>
    SystemSample Sample(int gamePid);
}

public interface IProcessWatcher
{
    bool IsRunning(int pid);
}

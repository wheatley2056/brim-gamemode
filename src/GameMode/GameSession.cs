using Brim.Core;

namespace Brim.GameMode;

/// <summary>Was nach dem Spiel kurz erscheint.</summary>
public sealed record GameSummary(string Name, TimeSpan Duration, double? AverageFps, double? PeakGpuTemperature);

/// <summary>Ein erkanntes Spiel, vom Erkennen bis sein Prozess endet; auch während es im Hintergrund liegt.</summary>
public sealed class GameSession(ForegroundWindow window, GameReason reason, DateTimeOffset startedAt)
{
    private double _fpsSum;
    private int _fpsSamples;

    public int Pid { get; } = window.Pid;
    public string Name { get; } = window.DisplayName.Length > 0 ? window.DisplayName : window.ProcessName;
    public string ProcessName { get; } = window.ProcessName;
    public GameReason Reason { get; } = reason;
    public DateTimeOffset StartedAt { get; } = startedAt;

    /// <summary>Liegt das Spiel gerade vorn?</summary>
    public bool InForeground { get; set; } = true;

    /// <summary>Letzte Bildrate; <c>null</c> im Hintergrund, beim Laden oder ohne Messung.</summary>
    public FrameStats? Frames { get; private set; }

    public SystemSample System { get; private set; } = SystemSample.Empty;

    public double? PeakGpuTemperature { get; private set; }

    /// <summary>Mittel der Bildraten aller gemessenen Sekunden.</summary>
    public double? AverageFps => _fpsSamples == 0 ? null : _fpsSum / _fpsSamples;

    public void SetFrames(FrameStats? stats)
    {
        Frames = stats;
        if (stats is { Fps: > 0 } s)
        {
            _fpsSum += s.Fps;
            _fpsSamples++;
        }
    }

    public void SetSystem(SystemSample sample)
    {
        System = sample;
        if (sample.GpuTemperature is { } t && (PeakGpuTemperature is null || t > PeakGpuTemperature))
        {
            PeakGpuTemperature = t;
        }
    }

    public GameSummary Summarize(DateTimeOffset now) => new(Name, now - StartedAt, AverageFps, PeakGpuTemperature);
}

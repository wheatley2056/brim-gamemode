using Brim.Core;
using Brim.Core.Plugins;
using Brim.Core.Widgets;

namespace Brim.GameMode.Tests;

internal sealed class RecordingSink : INotchSink
{
    public List<ActivityParams> Activities { get; } = [];
    public List<string> Ended { get; } = [];
    public List<HudParams> Huds { get; } = [];
    public List<(string Id, TileSize Size, Widget Content)> Widgets { get; } = [];
    public List<string> Logs { get; } = [];

    public ActivityParams? Last => Activities.Count > 0 ? Activities[^1] : null;

    public Task SetActivityAsync(ActivityParams activity)
    {
        Activities.Add(activity);
        return Task.CompletedTask;
    }

    public Task EndActivityAsync(string id)
    {
        Ended.Add(id);
        return Task.CompletedTask;
    }

    public Task ShowHudAsync(HudParams hud)
    {
        Huds.Add(hud);
        return Task.CompletedTask;
    }

    public Task LogAsync(string message, string level)
    {
        Logs.Add(message);
        return Task.CompletedTask;
    }

    public Task SetWidgetAsync(string id, TileSize size, Widget content)
    {
        Widgets.Add((id, size, content));
        return Task.CompletedTask;
    }
}

internal sealed class FakeForeground : IForegroundSource
{
    public ForegroundWindow? Window { get; set; }
    public ForegroundWindow? Read() => Window;
}

internal sealed class FakeFrames : IFrameSource
{
    public FrameSourceState State { get; set; } = FrameSourceState.Running;
    public string? Message { get; set; }
    public int Watched { get; private set; }

    /// <summary>Bildrate je Prozess; fehlt er, gibt er keine Bilder aus.</summary>
    public Dictionary<int, double> Fps { get; } = [];

    public void Watch(int pid) => Watched = pid;

    public FrameStats? Read() => Fps.TryGetValue(Watched, out var fps)
        ? new FrameStats(fps, fps * 0.8, 1000 / fps, [1000 / fps, 1000 / fps])
        : null;

    public void Dispose()
    {
    }
}

internal sealed class FakeSensors : ISensorSource
{
    public SystemSample Next { get; set; } = new(40, 95, 70, 6UL << 30);
    public SystemSample Sample(int gamePid) => Next;

    public void Dispose()
    {
    }
}

internal sealed class FakeProcesses : IProcessWatcher
{
    public HashSet<int> Running { get; } = [];
    public bool IsRunning(int pid) => Running.Contains(pid);
}

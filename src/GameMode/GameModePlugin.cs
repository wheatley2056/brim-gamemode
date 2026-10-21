using System.Globalization;
using System.Text.Json;
using Brim.Core;
using Brim.Core.Plugins;
using Brim.Core.Widgets;

namespace Brim.GameMode;

/// <summary>
/// Jede Sekunde: Vordergrund lesen, dessen Bildrate messen, ein Spiel erkennen und anzeigen. Ein erkanntes Spiel
/// bleibt die Sitzung, bis sein Prozess endet; danach kommt kurz das Fazit. Schickt nur, was sich geändert hat.
/// </summary>
public sealed class GameModePlugin
{
    public static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    /// <summary>Kürzere Spiele (Fehlstart, kurz reingeschaut) bekommen kein Fazit.</summary>
    public static readonly TimeSpan MinSummaryDuration = TimeSpan.FromMinutes(1);

    private readonly INotchSink _sink;
    private readonly IForegroundSource _foreground;
    private readonly IFrameSource _frames;
    private readonly ISensorSource _sensors;
    private readonly IProcessWatcher _processes;
    private readonly TimeProvider _time;
    private readonly Func<Task<bool>> _requestPermission;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, string> _sent = [];
    private IReadOnlyDictionary<string, IReadOnlyList<TileSize>> _placed = new Dictionary<string, IReadOnlyList<TileSize>>();

    private int _watched;
    private DateTimeOffset? _presentingSince;
    private string? _setupNote;
    private bool _activityShown;

    public GameModePlugin(INotchSink sink, IForegroundSource foreground, IFrameSource frames, ISensorSource sensors,
        IProcessWatcher processes, TimeProvider time, Func<Task<bool>> requestPermission, PluginSettings settings)
    {
        _sink = sink;
        _foreground = foreground;
        _frames = frames;
        _sensors = sensors;
        _processes = processes;
        _time = time;
        _requestPermission = requestPermission;
        Settings = settings;
    }

    public PluginSettings Settings { get; private set; }

    /// <summary>Das laufende Spiel, sonst <c>null</c>.</summary>
    public GameSession? Session { get; private set; }

    public GameSummary? LastGame { get; private set; }

    private static CultureInfo Culture => CultureInfo.CurrentCulture;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await TickAsync();
            try
            {
                await Task.Delay(Tick, _time, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task TickAsync()
    {
        await _gate.WaitAsync();
        try
        {
            var now = _time.GetUtcNow();
            if (Session is { } running && !_processes.IsRunning(running.Pid))
            {
                await EndSessionAsync(now);
            }

            var window = _foreground.Read();
            var pid = window?.Pid ?? 0;
            if (pid != _watched)
            {
                _watched = pid;
                _presentingSince = null;
                _frames.Watch(pid);
            }
            var stats = _frames.Read();
            if (stats is null)
            {
                _presentingSince = null;
            }
            else
            {
                _presentingSince ??= now;
            }

            if (window is not null && Session?.Pid != window.Pid
                && GameDetector.Detect(window, now - _presentingSince, stats?.Fps ?? 0, Settings) is { } reason)
            {
                if (Session is not null)
                {
                    await EndSessionAsync(now);
                }
                Session = new GameSession(window, reason, now);
                await _sink.LogAsync($"Spiel erkannt: {window.ProcessName} ({reason}, {window.ExePath ?? "Pfad unbekannt"})", "info");
            }

            if (Session is { } session)
            {
                session.InForeground = session.Pid == pid;
                session.SetFrames(session.InForeground ? stats : null);
                session.SetSystem(_sensors.Sample(session.Pid));
            }
            await RenderAsync(now);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EndSessionAsync(DateTimeOffset now)
    {
        var summary = Session!.Summarize(now);
        Session = null;
        LastGame = summary;
        await _sink.LogAsync($"Spiel beendet: {Views.SummaryText(summary, CultureInfo.InvariantCulture)}", "info");
        if (_activityShown)
        {
            await _sink.EndActivityAsync("game");
            _sent.Remove("activity");
            _activityShown = false;
        }
        if (Settings.Summary && summary.Duration >= MinSummaryDuration)
        {
            await _sink.ShowHudAsync(Views.SummaryHud(summary, Culture));
        }
    }

    private async Task RenderAsync(DateTimeOffset now)
    {
        if (Session is { } session)
        {
            var activity = Views.Activity(session, Settings, now, Culture);
            if (Changed("activity", activity))
            {
                await _sink.SetActivityAsync(activity);
                _activityShown = true;
            }
        }

        var status = Views.Status(_frames.State, _frames.Message, Session, LastGame, _setupNote, Culture);
        if (Changed("status", status))
        {
            await _sink.SetViewAsync("status", status, 10);
        }

        foreach (var size in _placed.GetValueOrDefault("game") ?? [])
        {
            var widget = Views.GameWidget(Session, LastGame, size, now, Culture);
            if (Changed("game:" + size, widget))
            {
                await _sink.SetWidgetAsync("game", size, widget);
            }
        }
    }

    /// <summary>Merkt sich, was zuletzt geschickt wurde; <c>true</c>, wenn <paramref name="value"/> neu ist.</summary>
    private bool Changed(string key, object value)
    {
        var json = JsonSerializer.Serialize(value, value.GetType(), PluginJson.Options);
        if (_sent.TryGetValue(key, out var sent) && sent == json)
        {
            return false;
        }
        _sent[key] = json;
        return true;
    }

    public async Task SetPlacedWidgetsAsync(IReadOnlyDictionary<string, IReadOnlyList<TileSize>> placed)
    {
        await _gate.WaitAsync();
        try
        {
            _placed = placed;
            await RenderAsync(_time.GetUtcNow());
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ApplySettingsAsync(PluginSettings settings)
    {
        await _gate.WaitAsync();
        try
        {
            Settings = settings;
            // Steht das laufende Spiel jetzt auf „Nie“, endet die Sitzung ohne Fazit.
            if (Session is { } session && settings.Exclude.Contains(session.ProcessName))
            {
                Session = null;
                await _sink.EndActivityAsync("game");
                _sent.Remove("activity");
                _activityShown = false;
            }
            await RenderAsync(_time.GetUtcNow());
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task HandleActionAsync(string actionId)
    {
        if (actionId != "setup")
        {
            return;
        }
        var ok = await _requestPermission();
        await _gate.WaitAsync();
        try
        {
            _setupNote = ok
                ? "Fertig. Melde dich einmal von Windows ab und wieder an, dann misst GameMode die Bildrate."
                : "Das hat nicht geklappt. Ohne Adminrechte lässt sich die Gruppe nicht setzen.";
            await RenderAsync(_time.GetUtcNow());
        }
        finally
        {
            _gate.Release();
        }
    }
}

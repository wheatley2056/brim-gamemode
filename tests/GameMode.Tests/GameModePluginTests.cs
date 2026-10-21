using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Brim.Core;
using Brim.Core.Plugins;
using Microsoft.Extensions.Time.Testing;

namespace Brim.GameMode.Tests;

public sealed class GameModePluginTests
{
    private const int GamePid = 1234;
    private static readonly ForegroundWindow Game = new(GamePid, "eldenring", "ELDEN RING", @"D:\steamapps\common\ELDEN RING\eldenring.exe", true);
    private static readonly ForegroundWindow Browser = new(77, "msedge", "Microsoft Edge", @"C:\Program Files\Edge\msedge.exe", false);

    private readonly RecordingSink _sink = new();
    private readonly FakeForeground _foreground = new();
    private readonly FakeFrames _frames = new();
    private readonly FakeSensors _sensors = new();
    private readonly FakeProcesses _processes = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 21, 20, 0, 0, TimeSpan.Zero));
    private int _permissionRequests;

    public GameModePluginTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
    }

    private GameModePlugin Create(PluginSettings? settings = null) => new(_sink, _foreground, _frames, _sensors, _processes, _time,
        () =>
        {
            _permissionRequests++;
            return Task.FromResult(true);
        }, settings ?? new PluginSettings());

    // Lesbar statt \u00B7 usw., damit die Tests nach Text suchen können.
    private static readonly JsonSerializerOptions Readable = new(PluginJson.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Json(object value) => JsonSerializer.Serialize(value, value.GetType(), Readable);

    private async Task StartGame(GameModePlugin plugin)
    {
        _processes.Running.Add(GamePid);
        _frames.Fps[GamePid] = 144;
        _foreground.Window = Game;
        await plugin.TickAsync();
    }

    [Fact]
    public async Task Game_in_front_shows_fps_in_the_notch_and_as_overlay_pill()
    {
        var plugin = Create();

        await StartGame(plugin);

        Assert.NotNull(plugin.Session);
        var activity = Assert.IsType<ActivityParams>(_sink.Last);
        Assert.Equal("game", activity.Id);
        Assert.True(activity.Overlay);
        Assert.False(activity.Background);
        Assert.Contains("144 FPS · 6,9 ms", Json(activity.Content!));
        Assert.Contains("95 %", Json(activity.Expanded!));
        Assert.Contains("70 °C", Json(activity.Expanded!));
        Assert.Contains("6,0 GB", Json(activity.Expanded!));
        Assert.Contains(_sink.Logs, l => l.Contains("eldenring"));
    }

    [Fact]
    public async Task Alt_tab_moves_the_game_to_the_background_without_pill()
    {
        var plugin = Create();
        await StartGame(plugin);

        _foreground.Window = Browser;
        _frames.Fps[Browser.Pid] = 60;
        await plugin.TickAsync();

        var activity = _sink.Last!;
        Assert.True(activity.Background);
        Assert.False(activity.Overlay);
        Assert.Contains("im Hintergrund", Json(activity.Expanded!));
        Assert.Equal(GamePid, plugin.Session!.Pid);

        _foreground.Window = Game;
        await plugin.TickAsync();
        Assert.True(_sink.Last!.Overlay);
    }

    [Fact]
    public async Task Overlay_setting_off_keeps_the_pill_away()
    {
        var plugin = Create(new PluginSettings { Overlay = false });

        await StartGame(plugin);

        Assert.False(_sink.Last!.Overlay);
    }

    [Fact]
    public async Task Ending_the_game_ends_the_activity_and_shows_a_summary()
    {
        var plugin = Create();
        await StartGame(plugin);
        for (var i = 0; i < 90; i++)
        {
            _frames.Fps[GamePid] = i % 2 == 0 ? 100 : 80;
            _time.Advance(TimeSpan.FromSeconds(1));
            await plugin.TickAsync();
        }

        _processes.Running.Remove(GamePid);
        _foreground.Window = null;
        await plugin.TickAsync();

        Assert.Null(plugin.Session);
        Assert.Contains("game", _sink.Ended);
        var hud = Assert.Single(_sink.Huds);
        Assert.Contains("ELDEN RING · 1 min · Ø 91 FPS · max. 70 °C", hud.Announcement);
        Assert.Contains("Zuletzt: ELDEN RING", Json(_sink.Views["status"]));
    }

    [Fact]
    public async Task Short_sessions_get_no_summary()
    {
        var plugin = Create();
        await StartGame(plugin);
        _time.Advance(TimeSpan.FromSeconds(20));

        _processes.Running.Remove(GamePid);
        await plugin.TickAsync();

        Assert.Empty(_sink.Huds);
        Assert.Contains("game", _sink.Ended);
    }

    [Fact]
    public async Task Browser_in_fullscreen_is_ignored()
    {
        var plugin = Create();
        _foreground.Window = Browser with { CoversMonitor = true };
        _frames.Fps[Browser.Pid] = 60;

        for (var i = 0; i < 10; i++)
        {
            _time.Advance(TimeSpan.FromSeconds(1));
            await plugin.TickAsync();
        }

        Assert.Null(plugin.Session);
        Assert.Empty(_sink.Activities);
        Assert.Contains("Wartet auf ein Spiel", Json(_sink.Views["status"]));
    }

    [Fact]
    public async Task Unknown_fullscreen_game_is_detected_after_the_delay()
    {
        var plugin = Create();
        var game = new ForegroundWindow(55, "indie", "Indie", @"C:\Program Files\Indie\indie.exe", true);
        _processes.Running.Add(55);
        _foreground.Window = game;
        _frames.Fps[55] = 60;

        await plugin.TickAsync();
        Assert.Null(plugin.Session);

        _time.Advance(GameDetector.FullscreenDelay);
        await plugin.TickAsync();
        Assert.Equal(GameReason.Fullscreen, plugin.Session!.Reason);
    }

    [Fact]
    public async Task Same_values_are_not_sent_twice()
    {
        var plugin = Create();
        await StartGame(plugin);
        var sent = _sink.Activities.Count;

        await plugin.TickAsync();

        Assert.Equal(sent, _sink.Activities.Count);
    }

    [Fact]
    public async Task Without_etw_rights_the_tab_offers_setup_and_system_values_still_show()
    {
        _frames.State = FrameSourceState.Denied;
        var plugin = Create(new PluginSettings { Include = PluginSettings.ParseNames("eldenring") });
        _processes.Running.Add(GamePid);
        _foreground.Window = Game;

        await plugin.TickAsync();

        Assert.Contains("Einrichten", Json(_sink.Views["status"]));
        Assert.Contains("95 %", Json(_sink.Last!.Expanded!));

        await plugin.HandleActionAsync("setup");
        Assert.Equal(1, _permissionRequests);
        Assert.Contains("ab und wieder an", Json(_sink.Views["status"]));
    }

    [Fact]
    public async Task Placed_widgets_get_content_in_each_size()
    {
        var plugin = Create();
        await plugin.SetPlacedWidgetsAsync(new Dictionary<string, IReadOnlyList<TileSize>> { ["game"] = [TileSize.Small, TileSize.Medium] });

        Assert.Equal(2, _sink.Widgets.Count);
        Assert.Contains("Kein Spiel", Json(_sink.Widgets[0].Content));

        await StartGame(plugin);
        Assert.Contains("144", Json(_sink.Widgets[^1].Content));
    }

    [Fact]
    public async Task Excluding_the_running_game_ends_it_without_summary()
    {
        var plugin = Create();
        await StartGame(plugin);
        _time.Advance(TimeSpan.FromMinutes(5));

        await plugin.ApplySettingsAsync(new PluginSettings { Exclude = PluginSettings.ParseNames("eldenring") });
        await plugin.TickAsync();

        Assert.Null(plugin.Session);
        Assert.Contains("game", _sink.Ended);
        Assert.Empty(_sink.Huds);
    }
}

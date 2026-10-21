namespace Brim.GameMode.Tests;

public sealed class GameDetectorTests
{
    private static readonly PluginSettings Defaults = new();
    private static readonly TimeSpan Long = TimeSpan.FromSeconds(30);

    private static ForegroundWindow Window(string name, string? path = null, bool fullscreen = true) =>
        new(42, name, name, path, fullscreen);

    [Fact]
    public void Steam_game_counts_as_soon_as_it_presents_even_windowed()
    {
        var game = Window("eldenring", @"D:\SteamLibrary\steamapps\common\ELDEN RING\Game\eldenring.exe", fullscreen: false);

        Assert.Equal(GameReason.GameFolder, GameDetector.Detect(game, TimeSpan.Zero, 60, Defaults));
        Assert.Null(GameDetector.Detect(game, null, 0, Defaults));
    }

    [Theory]
    [InlineData(@"C:\Program Files\Epic Games\Fortnite\FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe")]
    [InlineData(@"C:\XboxGames\Forza Horizon 5\Content\ForzaHorizon5.exe")]
    [InlineData(@"C:/GOG Games/Witcher 3/bin/x64/witcher3.exe")]
    [InlineData(@"E:\Games\Factorio\bin\x64\factorio.exe")]
    public void Launcher_folders_are_game_folders(string path) => Assert.True(GameDetector.InGameFolder(path));

    [Fact]
    public void Unknown_fullscreen_app_counts_after_a_few_seconds_of_frames()
    {
        var app = Window("SomeGame", @"C:\Program Files\Some\SomeGame.exe");

        Assert.Null(GameDetector.Detect(app, TimeSpan.FromSeconds(2), 120, Defaults));
        Assert.Equal(GameReason.Fullscreen, GameDetector.Detect(app, GameDetector.FullscreenDelay, 120, Defaults));
        Assert.Null(GameDetector.Detect(app, Long, 5, Defaults));
        Assert.Null(GameDetector.Detect(app with { CoversMonitor = false }, Long, 120, Defaults));
    }

    [Theory]
    [InlineData("chrome")]
    [InlineData("vlc")]
    [InlineData("steam")]
    [InlineData("POWERPNT")]
    public void Browsers_players_and_launchers_are_never_games(string name)
    {
        var app = Window(name, @"C:\Games\" + name + ".exe");

        Assert.Null(GameDetector.Detect(app, Long, 60, Defaults));
    }

    [Fact]
    public void Lists_win_and_never_beats_always()
    {
        var settings = new PluginSettings
        {
            Include = PluginSettings.ParseNames("emulator.exe, Chrome"),
            Exclude = PluginSettings.ParseNames("chrome; benchmark"),
        };

        Assert.Equal(GameReason.Listed, GameDetector.Detect(Window("Emulator", fullscreen: false), null, 0, settings));
        Assert.Null(GameDetector.Detect(Window("chrome"), Long, 60, settings));
        Assert.Null(GameDetector.Detect(Window("benchmark", @"C:\Games\benchmark.exe"), Long, 60, settings));
    }

    [Fact]
    public void Names_are_split_trimmed_and_lose_exe()
    {
        Assert.Equal(["a", "b", "c d"], PluginSettings.ParseNames(" a.EXE ,b;;\nc d ").Order());
        Assert.Empty(PluginSettings.ParseNames(null));
    }
}

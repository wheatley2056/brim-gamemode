using System.Globalization;

namespace Brim.GameMode.Tests;

public sealed class ViewsTests
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Fact]
    public void Durations_read_naturally()
    {
        Assert.Equal("1 min", Views.Duration(TimeSpan.FromSeconds(20), German));
        Assert.Equal("42 min", Views.Duration(TimeSpan.FromMinutes(42.7), German));
        Assert.Equal("1:05 h", Views.Duration(TimeSpan.FromMinutes(65), German));
    }

    [Fact]
    public void Bytes_switch_from_megabytes_to_gigabytes()
    {
        Assert.Equal("512 MB", Views.Bytes(512UL << 20, German));
        Assert.Equal("6,5 GB", Views.Bytes((ulong)(6.5 * (1UL << 30)), German));
        Assert.Equal("–", Views.Bytes(null, German));
    }

    [Fact]
    public void Pill_shows_temperature_only_when_asked()
    {
        var session = new GameSession(new ForegroundWindow(1, "game", "Game", null, true), GameReason.Listed, DateTimeOffset.UnixEpoch);
        session.SetFrames(new Brim.Core.FrameStats(59.6, 40, 16.78, []));
        session.SetSystem(new SystemSample(10, 20, 66.6, null));

        Assert.Equal("60 FPS · 16,8 ms", Views.PillText(session, false, German));
        Assert.Equal("60 FPS · 16,8 ms · 67 °C", Views.PillText(session, true, German));
    }
}

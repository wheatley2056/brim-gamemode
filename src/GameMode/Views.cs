using System.Globalization;
using Brim.Core;
using Brim.Core.Plugins;
using Brim.Core.Widgets;

namespace Brim.GameMode;

/// <summary>Alle Inhalte des Plugins als Widget-Bäume. Reine Funktionen, damit Tests sie prüfen können.</summary>
public static class Views
{
    /// <summary>Segoe Fluent Icons „Game“.</summary>
    public static readonly string GameGlyph = "";

    public const string Accent = "#7C9CFF";

    /// <summary>Ab hier wird die GPU-Temperatur gelb, ab <see cref="HotTemperature"/> rot.</summary>
    public const double WarmTemperature = 75;
    public const double HotTemperature = 85;

    // ───────── Texte ─────────

    public static string Fps(double fps, CultureInfo culture) => Math.Round(fps).ToString("0", culture);

    public static string Ms(double ms, CultureInfo culture) => ms.ToString("0.0", culture) + " ms";

    public static string Percent(double? value, CultureInfo culture) => value is { } v ? Math.Round(v).ToString("0", culture) + " %" : "–";

    public static string Temperature(double? celsius, CultureInfo culture) => celsius is { } c ? Math.Round(c).ToString("0", culture) + " °C" : "–";

    public static string Bytes(ulong? bytes, CultureInfo culture) => bytes switch
    {
        null => "–",
        >= 1UL << 30 => (bytes.Value / (double)(1UL << 30)).ToString("0.0", culture) + " GB",
        _ => (bytes.Value / (double)(1UL << 20)).ToString("0", culture) + " MB",
    };

    /// <summary>„42 min“, „1:05 h“.</summary>
    public static string Duration(TimeSpan duration, CultureInfo culture) => duration.TotalHours >= 1
        ? $"{(int)duration.TotalHours}:{duration.Minutes:00} h"
        : Math.Max(1, (int)duration.TotalMinutes).ToString(culture) + " min";

    /// <summary>„144 FPS · 6,9 ms“ (mit Temperatur: „144 FPS · 6,9 ms · 67 °C“).</summary>
    public static string PillText(GameSession session, bool withTemperature, CultureInfo culture)
    {
        var text = session.Frames is { } f ? $"{Fps(f.Fps, culture)} FPS · {Ms(f.FrameTimeMs, culture)}" : "– FPS";
        if (withTemperature && session.System.GpuTemperature is { } t)
        {
            text += " · " + Temperature(t, culture);
        }
        return text;
    }

    private static string TemperatureColor(double? celsius) => celsius switch
    {
        >= HotTemperature => BrimColors.Danger,
        >= WarmTemperature => BrimColors.Warning,
        _ => BrimColors.Fog,
    };

    // ───────── Aktivität ─────────

    /// <summary>
    /// Die Aktivität „game“: vorn kompakt mit Symbol und FPS, als Mini-Pille über dem Vollbild (Recht „overlay“),
    /// aufgeklappt mit allen Werten. Liegt das Spiel im Hintergrund, belegt sie die Notch nicht.
    /// </summary>
    public static ActivityParams Activity(GameSession session, PluginSettings settings, DateTimeOffset now, CultureInfo culture)
    {
        var pill = PillText(session, settings.PillTemperature, culture);
        return new ActivityParams
        {
            Id = "game",
            Priority = ActivityPriority.Ongoing,
            Leading = new IconWidget(GameGlyph) { Size = 14, Color = Accent },
            Trailing = new TextWidget(session.Frames is { } f ? Fps(f.Fps, culture) + " FPS" : "–") { Style = TextStyle.Mono },
            Content = new StackWidget(new TextWidget(pill) { Style = TextStyle.MonoCaption, FontSize = 11, Color = BrimColors.Fog })
            {
                Justify = Justify.Center,
                AccessibleName = pill,
            },
            Expanded = Expanded(session, now, culture),
            Rank = 5,
            Background = !session.InForeground,
            Overlay = settings.Overlay && session.InForeground,
        };
    }

    public static Widget Expanded(GameSession session, DateTimeOffset now, CultureInfo culture)
    {
        var f = session.Frames;
        var header = new StackWidget(
            new IconWidget(GameGlyph) { Size = 16, Color = Accent },
            new TextWidget(session.Name) { Style = TextStyle.Title, FontSize = 15, Grow = 1 },
            new TextWidget(session.InForeground ? Duration(now - session.StartedAt, culture) : "im Hintergrund") { Style = TextStyle.Secondary })
        {
            Spacing = 8,
        };
        var fps = new StackWidget(
            new TextWidget(f is null ? "–" : Fps(f.Fps, culture)) { Style = TextStyle.Display, FontSize = 34 },
            new TextWidget("FPS") { Style = TextStyle.Secondary },
            new SpacerWidget { Grow = 1 },
            new StackWidget(
                new TextWidget(f is null ? "1 % Low –" : $"1 % Low {Fps(f.OnePercentLow, culture)}") { Style = TextStyle.Secondary },
                new TextWidget(f is null ? "Frametime –" : $"Frametime {Ms(f.FrameTimeMs, culture)}") { Style = TextStyle.Secondary })
            {
                Orientation = StackOrientation.Vertical,
                Spacing = 2,
                Align = CrossAlign.End,
            })
        {
            Spacing = 8,
            Align = CrossAlign.End,
        };
        var children = new List<Widget> { header, fps };
        if (f is { FrameTimes.Count: > 1 })
        {
            children.Add(new ChartWidget(f.FrameTimes)
            {
                Capacity = FrameCounter.HistoryLength,
                Height = 40,
                Color = Accent,
                AccessibleName = "Frametime-Verlauf",
            });
        }
        children.Add(SystemRow(session.System, culture));
        return new StackWidget(children)
        {
            Orientation = StackOrientation.Vertical,
            Spacing = 8,
            Align = CrossAlign.Stretch,
        };
    }

    /// <summary>CPU, GPU, VRAM und GPU-Temperatur nebeneinander.</summary>
    public static Widget SystemRow(SystemSample s, CultureInfo culture) => new StackWidget(
        Gauge("CPU", Percent(s.Cpu, culture), s.Cpu / 100, BrimColors.Cpu),
        Gauge("GPU", Percent(s.Gpu, culture), s.Gpu / 100, BrimColors.Gpu),
        Stat("VRAM", Bytes(s.GameVram, culture), BrimColors.Fog),
        Stat("GPU-Temp.", Temperature(s.GpuTemperature, culture), TemperatureColor(s.GpuTemperature)))
    {
        Justify = Justify.SpaceBetween,
    };

    private static Widget Gauge(string label, string value, double? fraction, string color) => new StackWidget(
        new RingWidget(Math.Clamp(fraction ?? 0, 0, 1)) { Size = 22, Thickness = 3, Color = color, TrackColor = BrimColors.Track },
        Stat(label, value, BrimColors.Fog))
    {
        Spacing = 6,
        Grow = 1,
        AccessibleName = $"{label} {value}",
    };

    private static Widget Stat(string label, string value, string color) => new StackWidget(
        new TextWidget(value) { Style = TextStyle.Mono, FontSize = 13, Color = color },
        new TextWidget(label) { Style = TextStyle.Caption })
    {
        Orientation = StackOrientation.Vertical,
        Spacing = 0,
        Align = CrossAlign.Start,
        Grow = 1,
        AccessibleName = $"{label} {value}",
    };

    // ───────── Widget „Spiel“ auf „Jetzt“ ─────────

    public static Widget GameWidget(GameSession? session, GameSummary? last, TileSize size, DateTimeOffset now, CultureInfo culture)
    {
        if (session is null)
        {
            return Idle(last, size, culture);
        }
        var f = session.Frames;
        var fpsText = f is null ? "–" : Fps(f.Fps, culture);
        switch (size)
        {
            case TileSize.Small:
                return new StackWidget(
                    new TextWidget(fpsText) { Style = TextStyle.Display, FontSize = 26 },
                    new TextWidget("FPS") { Style = TextStyle.Secondary })
                {
                    Spacing = 6,
                    Align = CrossAlign.End,
                    AccessibleName = $"{session.Name}: {fpsText} FPS",
                };
            case TileSize.Medium or TileSize.Wide:
                return new StackWidget(
                    new StackWidget(
                        new TextWidget(fpsText) { Style = TextStyle.Display, FontSize = 26 },
                        new TextWidget(f is null ? "FPS" : "FPS · " + Ms(f.FrameTimeMs, culture)) { Style = TextStyle.Caption })
                    {
                        Orientation = StackOrientation.Vertical,
                        Spacing = 0,
                        Align = CrossAlign.Start,
                    },
                    new SpacerWidget { Grow = 1 },
                    Stat("GPU", Percent(session.System.Gpu, culture), BrimColors.Fog),
                    Stat("CPU", Percent(session.System.Cpu, culture), BrimColors.Fog),
                    Stat("Temp.", Temperature(session.System.GpuTemperature, culture), TemperatureColor(session.System.GpuTemperature)))
                {
                    Spacing = 10,
                    AccessibleName = $"{session.Name}: {fpsText} FPS",
                };
            default:
                return Expanded(session, now, culture);
        }
    }

    private static Widget Idle(GameSummary? last, TileSize size, CultureInfo culture)
    {
        var lines = new List<Widget>
        {
            new StackWidget(
                new IconWidget(GameGlyph) { Size = 16, Color = BrimColors.Slate },
                new TextWidget(size == TileSize.Small ? "Kein Spiel" : "Kein Spiel läuft") { Style = TextStyle.Secondary })
            {
                Spacing = 8,
            },
        };
        if (last is not null && size != TileSize.Small)
        {
            lines.Add(new TextWidget("Zuletzt: " + SummaryText(last, culture)) { Style = TextStyle.Caption, Wrap = size is TileSize.Large or TileSize.Full });
        }
        return new StackWidget(lines)
        {
            Orientation = StackOrientation.Vertical,
            Spacing = 4,
            Justify = Justify.Center,
            Align = CrossAlign.Start,
        };
    }

    // ───────── Tab „Plugins“ ─────────

    /// <summary>Karte im Tab „Plugins“: was gerade läuft, sonst das letzte Spiel; bei fehlenden Rechten die Einrichtung.</summary>
    public static Widget Status(FrameSourceState state, string? message, GameSession? session, GameSummary? last, string? setupNote, CultureInfo culture)
    {
        var children = new List<Widget>
        {
            new TextWidget("GameMode") { Style = TextStyle.Secondary, FontSize = 12 },
        };
        if (state == FrameSourceState.Denied)
        {
            children.Add(new TextWidget("FPS-Messung einrichten") { Style = TextStyle.Title, FontSize = 15 });
            children.Add(new TextWidget(setupNote ?? "Die Bildrate kommt wie bei PresentMon aus Windows-Ereignissen. Dafür muss dein Konto einmalig in die Gruppe „Leistungsprotokollbenutzer“; Windows fragt dazu nach Adminrechten. CPU, GPU und Temperatur gehen auch ohne.")
            {
                Style = TextStyle.Secondary,
                Wrap = true,
            });
            children.Add(new StackWidget(new ButtonWidget("setup") { Kind = ButtonKind.Secondary, Label = "Einrichten" }) { Justify = Justify.End });
        }
        else if (state == FrameSourceState.Failed)
        {
            children.Add(new TextWidget("FPS-Messung läuft nicht" + (message is { Length: > 0 } ? $": {message}" : "")) { Style = TextStyle.Secondary, Wrap = true });
        }
        if (session is not null)
        {
            var f = session.Frames;
            children.Add(new TextWidget(session.InForeground && f is not null
                ? $"{session.Name}: {Fps(f.Fps, culture)} FPS"
                : $"{session.Name} läuft") { Style = TextStyle.Body });
        }
        else if (state != FrameSourceState.Denied)
        {
            children.Add(new TextWidget("Wartet auf ein Spiel. Erkannt werden Vollbildspiele und alles aus Steam, Epic, GOG, Xbox und Co.") { Style = TextStyle.Secondary, Wrap = true });
            if (last is not null)
            {
                children.Add(new TextWidget("Zuletzt: " + SummaryText(last, culture)) { Style = TextStyle.Caption, Wrap = true });
            }
        }
        return new StackWidget(children)
        {
            Orientation = StackOrientation.Vertical,
            Spacing = 6,
            Align = CrossAlign.Stretch,
            Background = BrimColors.Graphite,
            CornerRadius = 14,
            Padding = (12, 10, 12, 10),
        };
    }

    // ───────── Fazit ─────────

    /// <summary>„Elden Ring · 1:05 h · Ø 92 FPS · max. 71 °C“.</summary>
    public static string SummaryText(GameSummary s, CultureInfo culture)
    {
        var text = $"{s.Name} · {Duration(s.Duration, culture)}";
        if (s.AverageFps is { } fps)
        {
            text += $" · Ø {Fps(fps, culture)} FPS";
        }
        if (s.PeakGpuTemperature is { } t)
        {
            text += $" · max. {Temperature(t, culture)}";
        }
        return text;
    }

    public static HudParams SummaryHud(GameSummary summary, CultureInfo culture)
    {
        var text = SummaryText(summary, culture);
        return new HudParams
        {
            Id = "summary",
            Content = new StackWidget(
                new IconWidget(GameGlyph) { Size = 16, Color = Accent },
                new TextWidget(text) { Style = TextStyle.Body, Grow = 1 })
            {
                Spacing = 12,
                Padding = (18, 0, 18, 0),
            },
            DurationMs = 6000,
            Announcement = text,
        };
    }
}

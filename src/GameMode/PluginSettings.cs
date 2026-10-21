using System.Text.Json;

namespace Brim.GameMode;

/// <summary>Einstellungen aus <c>plugin.json</c> mit den Werten des Nutzers. Schalter dürfen auch als Text kommen.</summary>
public sealed record PluginSettings
{
    /// <summary>Mini-Pille über dem Spiel (Recht „overlay“).</summary>
    public bool Overlay { get; init; } = true;

    public bool PillTemperature { get; init; }

    /// <summary>Nach dem Spiel kurz Dauer und Ø FPS zeigen.</summary>
    public bool Summary { get; init; } = true;

    /// <summary>Prozessnamen, die immer als Spiel gelten (ohne <c>.exe</c>, Groß-/Kleinschreibung egal).</summary>
    public IReadOnlySet<string> Include { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Prozessnamen, die nie als Spiel gelten; schlägt <see cref="Include"/>.</summary>
    public IReadOnlySet<string> Exclude { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static PluginSettings From(IReadOnlyDictionary<string, JsonElement> values)
    {
        var defaults = new PluginSettings();
        return new PluginSettings
        {
            Overlay = Flag(values, "overlay", defaults.Overlay),
            PillTemperature = Flag(values, "pillTemperature", defaults.PillTemperature),
            Summary = Flag(values, "summary", defaults.Summary),
            Include = Names(values, "include"),
            Exclude = Names(values, "exclude"),
        };
    }

    /// <summary>„Game.exe, other ; dritte“ → {game, other, dritte}.</summary>
    public static HashSet<string> ParseNames(string? text)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in (text ?? "").Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var name = part.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? part[..^4] : part;
            if (name.Length > 0)
            {
                names.Add(name);
            }
        }
        return names;
    }

    private static HashSet<string> Names(IReadOnlyDictionary<string, JsonElement> values, string key) =>
        values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? ParseNames(value.GetString())
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static bool Flag(IReadOnlyDictionary<string, JsonElement> values, string key, bool fallback)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return fallback;
        }
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            JsonValueKind.Number => value.GetDouble() != 0,
            _ => fallback,
        };
    }
}

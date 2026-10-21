using System.Globalization;

namespace Brim.GameMode;

/// <summary>Rechenregeln für die Leistungsindikatoren der GPU, wie sie der Task-Manager anwendet.</summary>
public static class SensorMath
{
    /// <summary>
    /// Die GPU mit der höchsten Auslastung aus <c>\GPU Engine(*)\Utilization Percentage</c>: pro Engine über alle
    /// Prozesse summiert, je Adapter das Maximum. Instanznamen:
    /// <c>pid_1234_luid_0x00000000_0x0000C3B1_phys_0_eng_3_engtype_3D</c>.
    /// </summary>
    /// <returns>LUID des Adapters (<c>luid_0x…_0x…</c>) und seine Auslastung in Prozent; <c>null</c> ohne Werte.</returns>
    public static (string Luid, double Utilization)? BusiestAdapter(IEnumerable<(string Name, double Value)> engines)
    {
        var perEngine = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in engines)
        {
            var start = name.IndexOf("luid_", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                continue;
            }
            var end = name.IndexOf("_engtype", start, StringComparison.OrdinalIgnoreCase);
            var key = end > start ? name[start..end] : name[start..];
            perEngine[key] = perEngine.GetValueOrDefault(key) + value;
        }
        (string Luid, double Utilization)? best = null;
        foreach (var group in perEngine.GroupBy(e => LuidOf(e.Key), StringComparer.OrdinalIgnoreCase))
        {
            var max = group.Max(e => e.Value);
            if (best is null || max > best.Value.Utilization)
            {
                best = (group.Key, Math.Clamp(max, 0, 100));
            }
        }
        return best;
    }

    /// <summary><c>luid_0x00000000_0x0000C3B1_phys_0_eng_3</c> → <c>luid_0x00000000_0x0000C3B1</c>.</summary>
    public static string LuidOf(string instance)
    {
        var start = instance.IndexOf("luid_", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return "";
        }
        var phys = instance.IndexOf("_phys", start, StringComparison.OrdinalIgnoreCase);
        return phys > start ? instance[start..phys] : instance[start..];
    }

    /// <summary><c>luid_0x00000000_0x0000C3B1</c> → (0, 0xC3B1); <c>null</c>, wenn es keine LUID ist.</summary>
    public static (int High, uint Low)? ParseLuid(string luid)
    {
        var parts = luid.Split('_');
        if (parts.Length < 3 || parts[0] != "luid"
            || !uint.TryParse(parts[1].Replace("0x", "", StringComparison.OrdinalIgnoreCase), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var high)
            || !uint.TryParse(parts[2].Replace("0x", "", StringComparison.OrdinalIgnoreCase), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var low))
        {
            return null;
        }
        return (unchecked((int)high), low);
    }

    /// <summary>Dedizierter Grafikspeicher eines Prozesses aus <c>\GPU Process Memory(*)\Dedicated Usage</c> (alle Adapter).</summary>
    public static ulong? ProcessVram(IEnumerable<(string Name, double Value)> memory, int pid)
    {
        var prefix = $"pid_{pid}_";
        double? sum = null;
        foreach (var (name, value) in memory)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && value > 0)
            {
                sum = (sum ?? 0) + value;
            }
        }
        return sum is { } s ? (ulong)s : null;
    }

    /// <summary>Der Treiber meldet Zehntelgrad; 0 heißt „kann er nicht“.</summary>
    public static double? Temperature(uint deciCelsius) => deciCelsius is 0 or > 2000 ? null : deciCelsius / 10.0;
}

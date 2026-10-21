namespace Brim.GameMode;

/// <summary>Warum ein Fenster als Spiel gilt (fürs Protokoll).</summary>
public enum GameReason
{
    /// <summary>Steht in der Liste „Immer als Spiel erkennen“.</summary>
    Listed,
    /// <summary>Liegt in einem Spieleordner (Steam, Epic, GOG, Xbox …) und gibt Bilder aus.</summary>
    GameFolder,
    /// <summary>Füllt den Monitor und gibt seit einigen Sekunden durchgehend Bilder aus.</summary>
    Fullscreen,
}

/// <summary>
/// Entscheidet, ob das Vordergrundfenster ein Spiel ist. Reine Funktion, damit sie vollständig testbar ist.
/// Reihenfolge: „Nie“-Liste und bekannte Nicht-Spiele, dann „Immer“-Liste, dann Spieleordner, dann Vollbild.
/// </summary>
public static class GameDetector
{
    /// <summary>So lange muss ein unbekanntes Vollbildfenster Bilder ausgeben, bevor es als Spiel gilt.</summary>
    public static readonly TimeSpan FullscreenDelay = TimeSpan.FromSeconds(5);

    /// <summary>Darunter ist es eher eine Diashow oder ein Ladebildschirm als ein Spiel.</summary>
    public const double MinFullscreenFps = 15;

    /// <summary>Ordner, in denen Launcher ihre Spiele installieren (Teile des Pfads, mit Trennzeichen).</summary>
    public static readonly IReadOnlyList<string> GameFolders =
    [
        @"\steamapps\common\",
        @"\Epic Games\",
        @"\GOG Galaxy\Games\",
        @"\GOG Games\",
        @"\XboxGames\",
        @"\Riot Games\",
        @"\Ubisoft Game Launcher\games\",
        @"\EA Games\",
        @"\Electronic Arts\",
        @"\Rockstar Games\",
        @"\Battle.net\Games\",
        @"\Games\",
    ];

    /// <summary>
    /// Apps, die auch im Vollbild Bilder ausgeben, aber keine Spiele sind: Browser, Videoplayer, Launcher,
    /// Streaming, Präsentationen, Brim selbst.
    /// </summary>
    public static readonly IReadOnlySet<string> NotGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Browser
        "chrome", "msedge", "firefox", "opera", "opera_gx", "brave", "vivaldi", "arc", "iexplore",
        // Video
        "vlc", "mpc-hc", "mpc-hc64", "mpc-be", "mpc-be64", "mpv", "PotPlayer", "PotPlayerMini", "PotPlayerMini64",
        "wmplayer", "Video.UI", "Microsoft.Media.Player", "PlexMediaPlayer", "Plex", "kodi", "Netflix", "Disney+",
        // Launcher, die selbst keine Spiele sind
        "steam", "steamwebhelper", "EpicGamesLauncher", "GalaxyClient", "Battle.net", "EADesktop", "Origin",
        "RiotClientServices", "RiotClientUx", "UbisoftConnect", "upc", "XboxPcApp", "GamingServices",
        // Aufnahme, Kommunikation, Arbeit
        "obs64", "obs32", "Discord", "Teams", "ms-teams", "Zoom", "POWERPNT", "Spotify",
        // Windows und Brim
        "explorer", "ApplicationFrameHost", "dwm", "LockApp", "ShellExperienceHost", "SearchHost", "StartMenuExperienceHost",
        "Brim", "BrimGameMode",
    };

    /// <param name="window">Vordergrundfenster.</param>
    /// <param name="presentingFor">So lange gibt es schon ohne Pause Bilder aus; <c>null</c> = gerade gar nicht.</param>
    /// <param name="fps">Aktuelle Bildrate.</param>
    /// <returns>Der Grund, wenn es ein Spiel ist, sonst <c>null</c>.</returns>
    public static GameReason? Detect(ForegroundWindow window, TimeSpan? presentingFor, double fps, PluginSettings settings)
    {
        var name = window.ProcessName;
        if (name.Length == 0 || settings.Exclude.Contains(name) || NotGames.Contains(name))
        {
            return null;
        }
        if (settings.Include.Contains(name))
        {
            return GameReason.Listed;
        }
        if (presentingFor is null)
        {
            return null;
        }
        if (InGameFolder(window.ExePath))
        {
            return GameReason.GameFolder;
        }
        if (window.CoversMonitor && presentingFor >= FullscreenDelay && fps >= MinFullscreenFps)
        {
            return GameReason.Fullscreen;
        }
        return null;
    }

    public static bool InGameFolder(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }
        var normalized = path.Replace('/', '\\');
        return GameFolders.Any(f => normalized.Contains(f, StringComparison.OrdinalIgnoreCase));
    }
}

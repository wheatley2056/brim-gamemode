using System.Diagnostics;
using System.Security.Principal;

namespace Brim.GameMode.Windows;

/// <summary>
/// Einrichtung, einmalig mit Adminrechten: Der Nutzer kommt in die Gruppe „Leistungsprotokollbenutzer“.
/// Danach darf das Plugin ETW ohne Adminrechte lesen. Dieselbe Gruppe wie für die eingebaute FPS-Messung von Brim.
/// </summary>
internal static class PerformanceLogUsers
{
    public const string Argument = "--setup";

    /// <summary>Bekannte SID der Gruppe; ihr Name hängt von der Systemsprache ab.</summary>
    private static readonly SecurityIdentifier Group = new("S-1-5-32-559");

    /// <summary>Startet das Plugin mit Adminrechten (Rückfrage der Benutzerkontensteuerung), um den Nutzer aufzunehmen.</summary>
    public static async Task<bool> RequestAsync()
    {
        var user = WindowsIdentity.GetCurrent().Name;
        try
        {
            using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = true,
                Verb = "runas",
                ArgumentList = { Argument, user },
            });
            if (process is null)
            {
                return false;
            }
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Rückfrage abgelehnt.
            return false;
        }
    }

    /// <summary>Läuft im erhöhten Prozess: <c>net localgroup &lt;Gruppe&gt; &lt;Nutzer&gt; /add</c>.</summary>
    public static int Add(string user)
    {
        var name = ((NTAccount)Group.Translate(typeof(NTAccount))).Value;
        var group = name[(name.IndexOf('\\') + 1)..];
        using var process = Process.Start(new ProcessStartInfo("net.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "localgroup", group, user, "/add" },
        });
        process!.WaitForExit();
        // 2 (Fehler 1378): schon Mitglied – auch gut.
        return process.ExitCode is 0 or 2 ? 0 : process.ExitCode;
    }
}

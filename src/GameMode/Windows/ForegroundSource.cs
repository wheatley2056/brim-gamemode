using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Brim.GameMode.Windows;

/// <summary>Liest das Vordergrundfenster: Prozess, Exe-Pfad und ob es seinen Monitor ganz füllt.</summary>
internal sealed partial class ForegroundSource : IForegroundSource
{
    private static readonly HashSet<string> ShellClasses = new(StringComparer.Ordinal) { "Progman", "WorkerW", "Shell_TrayWnd" };
    private readonly Dictionary<int, (string Name, string Display, string? Path)> _names = [];

    public unsafe ForegroundWindow? Read()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == 0)
        {
            return null;
        }
        var buffer = stackalloc char[64];
        var length = GetClassNameW(hwnd, buffer, 64);
        if (ShellClasses.Contains(new string(buffer, 0, length)))
        {
            return null;
        }
        _ = GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == Environment.ProcessId)
        {
            return null;
        }
        var monitor = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
        var info = new MonitorInfo { Size = (uint)sizeof(MonitorInfo) };
        var covers = GetMonitorInfoW(monitor, ref info) && GetWindowRect(hwnd, out var rect)
            && rect.Left <= info.Monitor.Left && rect.Top <= info.Monitor.Top
            && rect.Right >= info.Monitor.Right && rect.Bottom >= info.Monitor.Bottom;
        var (name, display, path) = Describe((int)pid);
        return new ForegroundWindow((int)pid, name, display, path, covers);
    }

    private (string Name, string Display, string? Path) Describe(int pid)
    {
        if (_names.TryGetValue(pid, out var known))
        {
            return known;
        }
        if (_names.Count > 128)
        {
            _names.Clear();
        }
        (string, string, string?) result = ("", "", null);
        try
        {
            using var process = Process.GetProcessById(pid);
            var name = process.ProcessName;
            var path = QueryPath(pid);
            var display = name;
            try
            {
                if (path is not null && FileVersionInfo.GetVersionInfo(path).FileDescription is { Length: > 0 and <= 40 } description)
                {
                    display = description.Trim();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
            result = (name, display, path);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
        }
        _names[pid] = result;
        return result;
    }

    /// <summary>
    /// Pfad über <c>QueryFullProcessImageName</c> mit eingeschränktem Zugriff: klappt auch bei Spielen mit
    /// Anti-Cheat, die <c>MainModule</c> verweigern.
    /// </summary>
    private static unsafe string? QueryPath(int pid)
    {
        var handle = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, (uint)pid);
        if (handle == 0)
        {
            return null;
        }
        try
        {
            var buffer = stackalloc char[1024];
            var size = 1024u;
            return QueryFullProcessImageNameW(handle, 0, buffer, ref size) ? new string(buffer, 0, (int)size) : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public uint Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    private static unsafe partial int GetClassNameW(nint hwnd, char* name, int max);

    [LibraryImport("user32.dll")]
    private static partial uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfoW(nint monitor, ref MonitorInfo info);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hwnd, out Rect rect);

    [LibraryImport("kernel32.dll")]
    private static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageNameW(nint process, uint flags, char* name, ref uint size);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}

/// <summary>Läuft ein Prozess noch? Über ein Handle mit eingeschränktem Zugriff und seinen Exit-Code.</summary>
internal sealed partial class ProcessWatcher : IProcessWatcher
{
    private const uint StillActive = 259;

    public bool IsRunning(int pid)
    {
        var handle = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, (uint)pid);
        if (handle == 0)
        {
            // Zugriff verweigert heißt: es gibt ihn noch (z. B. erhöht gestartet).
            return Marshal.GetLastPInvokeError() == 5;
        }
        try
        {
            return GetExitCodeProcess(handle, out var code) && code == StillActive;
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint pid);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetExitCodeProcess(nint process, out uint code);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);
}

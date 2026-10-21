// GameMode-Plugin für Brim: erkennt laufende Spiele und zeigt FPS, Frametime, CPU-/GPU-Last, VRAM und
// GPU-Temperatur in der Notch, im Vollbild als Mini-Pille. Mit --setup <Nutzer> (erhöht gestartet) richtet es
// einmalig die Gruppe „Leistungsprotokollbenutzer“ ein.
using Brim.GameMode;
using Brim.GameMode.Windows;
using Brim.Sdk;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("Das GameMode-Plugin läuft nur unter Windows.");
    return 1;
}

if (args is [PerformanceLogUsers.Argument, var user])
{
    return PerformanceLogUsers.Add(user);
}

BrimPlugin brim;
try
{
    brim = await BrimPlugin.ConnectAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Keine Verbindung zu Brim: {ex.Message}");
    return 1;
}

await using (brim)
{
    using var frames = new EtwFrameSource();
    frames.Start();
    Console.WriteLine($"FPS-Messung: {frames.State} {frames.Message}");
    using var sensors = new SensorSource();
    var plugin = new GameModePlugin(new BrimSink(brim), new ForegroundSource(), frames, sensors, new ProcessWatcher(),
        TimeProvider.System, PerformanceLogUsers.RequestAsync, PluginSettings.From(brim.Settings));

    brim.ActionInvoked += (action, value) => _ = plugin.HandleActionAsync(action);
    brim.SettingsChanged += settings => _ = plugin.ApplySettingsAsync(PluginSettings.From(settings));
    brim.WidgetsPlaced += placed => _ = plugin.SetPlacedWidgetsAsync(placed);
    _ = plugin.SetPlacedWidgetsAsync(brim.PlacedWidgets);

    using var stop = new CancellationTokenSource();
    var loop = plugin.RunAsync(stop.Token);
    await brim.RunAsync();

    // Brim gibt 2 Sekunden zum Beenden.
    stop.Cancel();
    await Task.WhenAny(loop, Task.Delay(500));
}
return 0;

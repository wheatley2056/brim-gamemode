# GameMode-Plugin für Brim

Merkt von selbst, wenn ein Spiel läuft, und zeigt in der Notch, wie es läuft:

- **FPS, 1 % Low und Frametime** mit Verlauf, gemessen wie bei [PresentMon](https://github.com/GameTechDev/PresentMon) über Windows-Ereignisse (ETW, DXGI und D3D9).
- **CPU- und GPU-Last, VRAM des Spiels und GPU-Temperatur**, mit denselben Werten wie im Task-Manager.
- **Mini-Pille über dem Spiel:** Zieht sich die Notch im Vollbild zurück, bleibt eine kleine Pille mit „144 FPS · 6,9 ms“ stehen (auf Wunsch mit GPU-Temperatur). Braucht Brim 2.3.1 und das Recht „Mini-Pille über Vollbild-Apps“.
- **Kompakt:** Läuft das Spiel im Fenster, steht ein Gamepad mit den FPS in der Notch. Aufgeklappt zeigt der Tab „Jetzt“ alle Werte.
- **Widget „Spiel“** für „Jetzt“ (klein, mittel, groß).
- **Fazit:** Nach dem Spiel kurz Dauer, Ø FPS und höchste GPU-Temperatur, z. B. „ELDEN RING · 1:05 h · Ø 92 FPS · max. 71 °C“.

## Wie ein Spiel erkannt wird

Das Fenster im Vordergrund gilt als Spiel, wenn

1. sein Prozess in der Liste **„Immer als Spiel erkennen“** steht, oder
2. seine Exe in einem Spieleordner liegt (Steam `steamapps\common`, Epic Games, GOG, Xbox `XboxGames`, Riot, Ubisoft, EA, Rockstar, Battle.net oder ein Ordner `Games`) und es Bilder ausgibt, oder
3. es den Monitor ganz füllt (Vollbild oder randlos) und seit 5 Sekunden mit mindestens 15 FPS Bilder ausgibt.

Browser, Videoplayer, Launcher, OBS, Discord, PowerPoint und ähnliche zählen nie, ebenso alles aus **„Nie als Spiel erkennen“**. Ein erkanntes Spiel bleibt es, bis sein Prozess endet; per Alt+Tab im Hintergrund belegt es die Notch nicht.

## Rechte

Brim fragt beim Einschalten nach dem Recht **„Mini-Pille über Vollbild-Apps und Spielen“**.

Die Bildrate liest das Plugin aus Windows-Ereignissen. Das dürfen Admins und Mitglieder der Gruppe **„Leistungsprotokollbenutzer“**. Fehlt das Recht, zeigt die Karte im Tab „Plugins“ **Einrichten**: Windows fragt einmal nach Adminrechten und nimmt dein Konto in die Gruppe auf, danach einmal ab- und wieder anmelden. Hast du die FPS-Messung in Brim (Einstellungen → Spiele) schon eingerichtet, ist das erledigt. CPU, GPU und Temperatur gehen auch ohne.

Eine CPU-Temperatur zeigt das Plugin nicht: Windows liefert sie zuverlässig nur über einen Kerneltreiber.

## Installieren

**Aus der Bibliothek:** Brim → Einstellungen → Plugins → Bibliothek → GameMode → Installieren.

**Fertig gebaut:** `brim-gamemode.zip` aus dem neuesten Release nach `%LocalAppData%\Brim\Plugins\gamemode` entpacken, Brim neu starten und im Tab **Plugins** „GameMode“ einschalten.

**Selbst bauen:** Das Plugin nutzt `Brim.Sdk` aus dem Brim-Repository. Beide Repositories nebeneinander klonen:

```powershell
git clone https://github.com/wheatley2056/brim.git
git clone https://github.com/wheatley2056/brim-gamemode.git
cd brim-gamemode
dotnet publish src/GameMode -c Release -r win-x64 --self-contained false -o "$env:LOCALAPPDATA\Brim\Plugins\gamemode"
```

Liegt Brim woanders: `-p:BrimRoot=C:\Pfad\zu\brim`.

## Einstellungen

Brim → Einstellungen → Plugins → GameMode:

- **FPS-Pille über dem Spiel** (an)
- **GPU-Temperatur in der Pille** (aus)
- **Fazit nach dem Spiel** (an): nur nach Spielen ab einer Minute.
- **Immer als Spiel erkennen** / **Nie als Spiel erkennen:** Prozessnamen mit Komma getrennt, z. B. `yuzu, retroarch` oder `blender`. Den Namen findest du im Task-Manager unter Details (ohne `.exe`).

Nutzt du zusätzlich die eingebaute FPS-Messung von Brim (Einstellungen → Spiele), erscheinen zwei Pillen. Schalte dort die Mini-Pille aus oder die Messung ganz ab.

## Wenn etwas nicht klappt

Das Protokoll liegt in `%LocalAppData%\Brim\logs\plugins\de.brim.gamemode.log`. Dort steht, ob die FPS-Messung läuft („FPS-Messung: Running“) und welches Spiel aus welchem Grund erkannt wurde, z. B. „Spiel erkannt: eldenring (GameFolder, …)“.

- **Spiel wird nicht erkannt:** in „Immer als Spiel erkennen“ eintragen.
- **Etwas anderes wird als Spiel erkannt:** in „Nie als Spiel erkennen“ eintragen.
- **Keine GPU-Temperatur:** Der Grafiktreiber muss sie melden (WDDM 2.4 oder neuer); im Task-Manager steht sie dann unter Leistung → GPU.
- **Keine FPS bei Vulkan- oder OpenGL-Spielen:** Gezählt werden DXGI- und D3D9-Presents. Vulkan-Spiele über DXGI-Interop (die meisten unter Windows 11) gehen, reines OpenGL meist nicht.

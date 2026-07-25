# HorosSaver — Portable Edition

**HorosSaver** von **HorosCode** als portabler Ordner — ohne Installer. Den gesamten Ordner kopieren, auf einem neuen PC `starter.bat` starten, Snapshots und Profile bleiben erhalten.

## Ordnerstruktur

```
HorosSaver/
├── HorosSaver.exe          # Self-contained (.NET 9 Desktop Runtime enthalten)
├── starter.bat             # Startskript (setzt Portable-Modus)
├── portable.txt            # Marker: portable Modus aktiv
├── README-PORTABLE.md      # Diese Datei
├── zu saven                # Optional: Seed-Liste für Programme/Ordner
├── data/                   # Alle Benutzerdaten (kopierbar)
│   ├── profiles.json
│   ├── settings.json
│   └── snapshots/
│       └── {programId}/
│           └── {snapshotId}/
└── logs/                   # App- und Starter-Logs
    ├── horossaver-YYYYMMDD.log
    ├── starter.log
    └── bootstrap.log       # Stille Abhaengigkeits-Pruefung/Installation
```

## Starten

1. Ordner beliebig hinlegen (z. B. `D:\Tools\HorosSaver\`)
2. Doppelklick auf **`starter.bat`**
3. `starter.bat` legt `data\` und `logs\` an, prüft Abhängigkeiten still und startet die GUI

Screenshots der Oberfläche (Programme, Snapshots, Timeline, Einstellungen): siehe Abschnitt **Screenshots** in [`README.md`](README.md).

**Kein .NET auf dem Ziel-PC nötig** — die portable Ausgabe ist self-contained. `starter.bat` überspringt vorhandene Komponenten und installiert fehlende Abhängigkeiten nur bei Bedarf (still via winget: .NET 9 Desktop Runtime, VC++ x64).

## Daten & Restore auf neuem System

| Inhalt | Pfad |
|---|---|
| Profile | `data\profiles.json` |
| Einstellungen | `data\settings.json` |
| Snapshots | `data\snapshots\{programId}\{snapshotId}\` |
| App-Logs | `logs\horossaver-*.log` |
| Starter-Log | `logs\starter.log` |
| Bootstrap-Log | `logs\bootstrap.log` |

## Logging

HorosSaver protokolliert App-Ereignisse in **täglich rotierenden Dateien** (`logs\horossaver-YYYYMMDD.log`). Der Starter schreibt nach `logs\starter.log`, stille Abhängigkeitsprüfungen nach `logs\bootstrap.log`.

**Typische Einträge in `horossaver-*.log`:**

| Ereignis | Level |
|---|---|
| App-Start, Portable-/Datenpfad | INFO |
| Snapshot in Warteschlange / gestartet / erfolgreich | INFO |
| Snapshot pausiert / fortgesetzt | INFO |
| Snapshot fehlgeschlagen (voller Fehlertext, z. B. gesperrte Datei) | ERROR |
| Snapshot abgebrochen | WARN |
| Unbehandelte Ausnahmen | FATAL |

In der UI werden Snapshot-Fehler gekürzt angezeigt (z. B. `Datei gesperrt: EditorServices.log`); der **vollständige Text** steht im Log und im Tooltip der Fehlerzeile. Die Statusleiste zeigt die Kurzmeldung.

Den **gesamten Ordner** kopieren (inkl. `data\`). Auf dem neuen PC `starter.bat` starten — Restore-Wizard nutzt die lokalen Snapshots.

## Portable-Erkennung (App)

Die App verwendet `{AppDir}\data\`, wenn eine der Bedingungen zutrifft:

- `starter.bat` setzt `HOROSSAVER_PORTABLE=1` und `HOROSSAVER_DATA_ROOT`
- Datei `portable.txt` oder `HorosSaver.portable` liegt neben `HorosSaver.exe`
- Umgebungsvariable `HOROSSAVER_DATA_ROOT` zeigt auf einen Ordner

Ohne diese Marker (z. B. `dotnet run` in der Entwicklung): Daten unter `%LocalAppData%\HorosCode\HorosSaver\`.

## Migration von alter Installation

Frühere Installer-Versionen speicherten unter:

`%LocalAppData%\HorosCode\HorosSaver\`

Wenn `data\` leer ist, aber dort noch Daten liegen: `profiles.json`, `settings.json` und den Ordner `snapshots\` manuell nach `data\` kopieren. Die App schreibt einen Hinweis ins Log (`logs\horossaver-*.log`).

## Fehlerbehebung

| Problem | Lösung |
|---|---|
| `HorosSaver.exe nicht gefunden` | Paket mit `scripts\HorosSaver.bat build` bauen |
| Fehlende DLL / VC++ | `starter.bat` versucht still `Microsoft.VCRedist.2015+.x64` via winget |
| Framework-dependent Build (selten) | `starter.bat` versucht still `Microsoft.DotNet.DesktopRuntime.9` via winget |
| winget fehlt | Abhängigkeiten manuell installieren (siehe Links unten) |
| Details | `logs\bootstrap.log`, `logs\starter.log` und `logs\horossaver-*.log` prüfen |

## Neu bauen (Entwickler)

```powershell
cd d:\HorosSaver\HorosSaver\HorosSaver
.\scripts\HorosSaver.bat build
```

Output: `artifacts\portable\HorosSaver\`

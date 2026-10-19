# Validierung — Godot 4.7.2 .NET

Stand: 19. Oktober 2026. Ausgangspunkt: `87ba1c2e5bcf421d0442969c64f62f7056cf7f1f` (`main` und `v0.3.0`). Der bestehende Release samt Tag und Dateien wurde nicht verändert.

## Verifizierte Version

- [Offizieller Download](https://godotengine.org/download/archive/4.7.2-stable/) und [GitHub-Release](https://github.com/godotengine/godot-builds/releases/tag/4.7.2-stable): 4.7.2-stable, veröffentlicht am 18. August 2026, kein Vorabrelease.
- Lokal ausgeführte Binary: `4.7.2.stable.mono.official.ed1daf0bf`.
- Linux-.NET-Editor SHA-256: `129f82db7bafd54ae14bb5bb284041c73860e8c7a009a3a026ca5e946cbff247`.
- .NET-Exportvorlagen SHA-256: `92f8681e349ef1f90891b792da95e3b2b0bd1ed610b78018c58feb2d87e15a9d`.
- Beide Downloads lokal gegen die offiziellen Asset-Digests geprüft; dieselben Prüfungen stehen im Workflow.

## Kompatibilität und lokale Ergebnisse

Alle acht C#-Dateien (Steuerung, Daten/Missionen, Spiel/Selbsttests, HUD, Projektile, Schiffe, Audio und Grafik) und die [Migrationshinweise 4.6](https://docs.godotengine.org/en/4.7/tutorials/migrating/upgrading_to_godot_4.6.html) / [4.7](https://docs.godotengine.org/en/4.7/tutorials/migrating/upgrading_to_godot_4.7.html) geprüft. Keine erforderlichen C#-API-Anpassungen gefunden; vollständige Neukompilierung gegen 4.7.2 statt Übernahme alter Assemblies. `net8.0`, Compatibility-Renderer, Steuerungsbelegung und Spielprofilpfad bleiben erhalten. Die sieben Modell-Importdateien enthalten die vom neuen Editor ergänzten Optionen.

- .NET SDK 8.0.426 / Godot.NET.Sdk 4.7.2: Restore und Build erfolgreich, **0 Warnungen, 0 Fehler**.
- `Tools/check_fleet_assets.py`: **7 gültige Modelle**.
- Godot-4.7.2-.NET-Import erfolgreich.
- Quellprojekt: **SELF TEST PASS: 3357 checks**.
- Reguläres `build.sh`: Linux-x86_64-Export mit mitgelieferter .NET-Laufzeit erfolgreich, ohne manuelles Zusammenführen von Publish-Dateien.
- Exportiertes Spiel: **SELF TEST PASS: 3357 checks**.
- Headless-Smoke-Test des exportierten Spiels mit `--smoke-test` über 480 Frames.

Die lokale Sandbox benötigt beschreibbare `DOTNET_CLI_HOME`, `NUGET_PACKAGES`, `XDG_CONFIG_HOME` und `XDG_DATA_HOME` im Arbeitsverzeichnis. NuGet benötigte dort seinen Migrationsmarker. Der Editor meldete bei Build/Export zusätzlich einen Fehler beim Erstellen seines ObjectDB-Snapshot-Verzeichnisses; der Export und die Tests waren trotzdem erfolgreich. Beim Beenden der Selbsttests bleibt eine ObjectDB-Leak-Warnung bestehen. Keine Grafik- oder Audioprüfung aus einem Headless-Lauf ableiten.

## Vor Freigabe manuell prüfen

- Steam Deck / SteamOS im Gaming-Modus: nativer Start ohne Proton, Controller inklusive Trigger, Fokus, Pause und Optionen.
- Alle drei Missionstypen vollständig spielen: Start, Kampf, Rückflug/Landung, Debrief und Wiederholung.
- Compatibility-Grafik bei 1280 × 800: Cockpit, sieben Modelle, HUD, Materialien, Effekte und Zielbildrate; aktuelle Screenshots mit der 4.5-Basis vergleichen.
- Audio, Stummschaltung, vorhandenes `pilot.cfg`, Optionen und Bestleistungen nach Neustart.
- Längere Kampfsitzung und Speicherprofil zur ObjectDB-Warnung.

Der GitHub-Workflow prüft Build, Import und beide Selbsttestläufe auch bei vorhandenem 0.3-Release. Pushes/PRs veröffentlichen nichts. Der manuelle Release-Schritt erzeugt nur einen noch nicht vorhandenen Release.

---

Die folgenden Ergebnisse dokumentieren ausschließlich die bisherige Godot-4.5-Version:

# Validierung — Space Fleet 0.3

Stand: 15. Oktober 2026.

## Lokal ausgeführt

- C#-Kompilierung mit .NET SDK 8.0.414, regulärem MSBuild und Godot.NET.Sdk 4.5.0; zusätzliche direkte Roslyn-Kompilierung für die Bildprüfung.
- Selbstständige Linux-x86_64-Veröffentlichung mit .NET-Laufzeit über MSBuild/Publish. Godot-4.5-.NET-Release-Export des Ressourcenpakets; die selbstständig veröffentlichten .NET-Dateien wurden anschließend in den vorgesehenen Datenordner übernommen.
- Start der exportierten Anwendung ohne DOTNET_ROOT und ohne Godot-Editor: **3.357 bestandene Prüfungen auch im nativen Linux-Spielpaket.**
- Erfolgreicher Godot-Import aller sieben GLB-Modelle.
- **3.357 bestandene automatisierte Spielprüfungen** im Quellprojekt. Dazu gehören 300 generierte Missionen, reproduzierbare Routen, Schiffszuteilung, Trefferberechnung, Schilde, Torpedoverbrauch und -treffer, alle drei Missionsabläufe bis zum Rückflug bzw. zur Landesequenz sowie die Niederlage bei Verlust der eskortierten Bomber.
- Modellintegration für Kestrel, Vanguard, Atlas, Raider, Tyrant, Resolute und Cockpit geprüft.
- `Tools/check_fleet_assets.py`: sieben gültige GLB-Dateien; endliche Koordinaten, passende Normalen, gültige Binärbereiche, Dreiecks-/Materialbudgets und freier Startkorridor der Resolute.
- Alle sieben Modelle zweimal erzeugt und die SHA-256-Werte verglichen: byte-identische Ausgabe.
- Reale OpenGL-Darstellung mit Mesa llvmpipe im virtuellen X11-Display bei 1280 × 800: neue Außenmodelle und Cockpit geprüft. Die `fleet-vanguard.png`, `fleet-atlas.png`, `fleet-raider.png`, `fleet-resolute.png`, `fleet-tyrant.png` und `fleet-cockpit.png` unter `docs/screenshots` zeigen Version 0.3 direkt aus Godot.
- Grafikprüfung aus der exportierten Anwendung: Resolute-Aufnahme erfolgreich; zusätzlicher OpenGL-Smoke-Test über 480 Frames einschließlich Übergang vom Trägerstart zum Cockpit.
- `git diff --check` ohne Fehler.

## Release-Prüfung

Der GitHub-Workflow für `v0.3.0` baut mit dem regulären Godot.NET.Sdk, importiert die Assets, führt die Spielprüfungen aus und exportiert eine native Linux-x86_64-Anwendung mit enthaltener .NET-Laufzeit. Anschließend werden dieselben Spielprüfungen in der exportierten Anwendung ausgeführt. Das ZIP wird erst veröffentlicht, wenn beide Testläufe erfolgreich sind. Der jeweilige Lauf ist unter GitHub Actions einsehbar.

## Modellbudgets

| Modell | Dreiecke | Materialflächen |
| --- | ---: | ---: |
| Kestrel | 4.722 | 12 |
| Vanguard | 2.724 | 10 |
| Atlas | 3.224 | 9 |
| Raider | 1.554 | 8 |
| Resolute | 9.996 | 8 |
| Tyrant | 10.488 | 7 |
| Cockpit | 4.308 | 9 |

Keine externen Texturen oder Modellpakete. Die Generatorskripte und Modelle stehen unter MIT.

## Grenzen

Ein echtes Steam Deck, reale Controllerhardware, Steam-Gaming-Modus, ausführliche Spielbalance und lange Kampfsitzungen wurden hier nicht geprüft. Die Zielbildrate ist deshalb noch nicht nachgewiesen. Trefferkörper bleiben vereinfachte Kugeln. Die sichtbaren Geschütztürme sind statische Modellteile, keine separat simulierten Waffen.

Im lokalen Editor verhindert die eingeschränkte Prozessumgebung gelegentlich den Aufruf von `dotnet publish`. Der reguläre SDK-Build und Publish wurden deshalb direkt über MSBuild ausgeführt; die veröffentlichten Dateien wurden im Export-Datenordner zusammengeführt und dort erneut getestet. Der bestehende GitHub-Workflow baut außerhalb dieser lokalen Einschränkung mit den üblichen Godot-Exportbefehlen. Godot meldet gelegentlich `ObjectDB instances leaked at exit`; ein Langzeit-Speicherprofil steht aus. Der virtuelle Grafiktreiber unterstützt keinen VSync-Wechsel.

## Reproduzieren

```bash
python3 Tools/check_fleet_assets.py
dotnet build SpaceFleet.csproj
godot --headless --path . --editor --import --quit
godot --headless --path . --quit-after 600 -- --self-test
```

GLB-Dateien neu erzeugen (zusätzlich numpy und scipy erforderlich):

```bash
python3 -m pip install numpy scipy
python3 Tools/build_fleet_assets.py
python3 Tools/check_fleet_assets.py
```

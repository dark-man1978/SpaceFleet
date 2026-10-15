# Validierung — Space Fleet 0.3

Stand: 15. Oktober 2026.

## Lokal ausgeführt

- C#-Kompilierung mit .NET SDK 8.0.414 / Roslyn und Godot-4.5-Quellgenerator.
- Erfolgreicher Godot-Import aller sieben GLB-Modelle.
- **3.357 bestandene automatisierte Spielprüfungen** im Quellprojekt. Dazu gehören 300 generierte Missionen, reproduzierbare Routen, Schiffszuteilung, Trefferberechnung, Schilde, Torpedoverbrauch und -treffer, alle drei Missionsabläufe bis zum Rückflug bzw. zur Landesequenz sowie die Niederlage bei Verlust der eskortierten Bomber.
- Modellintegration für Kestrel, Vanguard, Atlas, Raider, Tyrant, Resolute und Cockpit geprüft.
- `Tools/check_fleet_assets.py`: sieben gültige GLB-Dateien; endliche Koordinaten, passende Normalen, gültige Binärbereiche, Dreiecks-/Materialbudgets und freier Startkorridor der Resolute.
- Alle sieben Modelle zweimal erzeugt und die SHA-256-Werte verglichen: byte-identische Ausgabe.
- Reale OpenGL-Darstellung mit Mesa llvmpipe im virtuellen X11-Display bei 1280 × 800: neue Außenmodelle und Cockpit geprüft. Die `fleet-vanguard.png`, `fleet-atlas.png`, `fleet-raider.png`, `fleet-resolute.png`, `fleet-tyrant.png` und `fleet-cockpit.png` unter `docs/screenshots` zeigen Version 0.3 direkt aus Godot.
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

Im lokalen Editor kann die eingeschränkte Prozessumgebung die MSBuild-Erkennung verhindern; deshalb wurde lokal zusätzlich direkt mit dem Godot-Quellgenerator kompiliert. Der reguläre SDK-Build wird im Release-Workflow geprüft. Godot meldet gelegentlich `ObjectDB instances leaked at exit`; ein Langzeit-Speicherprofil steht aus. Der virtuelle Grafiktreiber unterstützt keinen VSync-Wechsel.

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

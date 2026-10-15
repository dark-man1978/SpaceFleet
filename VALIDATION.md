# Validierung — Space Fleet 0.2

Stand: 15. Oktober 2026.

## Grafikschritt 0.2

- Godot-Import der beiden GLB-Modelle erfolgreich.
- Vier zusätzliche Prüfungen für Modellressourcen und Szenenintegration, insgesamt 3.352 Prüfungen.
- Kestrel: 4.722 Dreiecke / 12 Materialflächen; Cockpit: 4.308 / 9.
- Außenmodell und Cockpit bei 1280 × 800 im virtuellen OpenGL-Display geprüft. HUD-Anzeigen stehen außerhalb der mechanischen Bedienelemente.
- Der native Linux-Build wird zusätzlich vom GitHub-Release-Workflow gebaut und getestet. Die Referenz zum Lauf steht unter den Actions des Release-Commits.
- Die älteren Aufnahmen unter `docs/screenshots/menu.png`, `briefing.png`, `launch.png` und `flight.png` dokumentieren Version 0.1. Die beiden `fleet-*.png` zeigen Version 0.2.

## Ausgeführt

- C#-Kompilierung mit Godot.NET.Sdk 4.5.0, .NET SDK 8.0.414, Ziel net8.0.
- Selbstständige Veröffentlichung für Linux x86_64 einschließlich .NET-Laufzeit.
- Godot-4.5-.NET-Release-Export mit eingebettetem Ressourcenpaket.
- Start der exportierten Linux-Binary ohne DOTNET_ROOT oder Godot-Editor.
- **3.352 bestandene automatisierte Prüfungen in der exportierten Version.**
- 300 generierte Missionen: 100 Seeds je Missionstyp, sichere Einstiegspunkte, Abstände, Reproduzierbarkeit, passende Schiffsklasse und garantierte Hauptziele.
- Kontinuierliche Projektil-Trefferberechnung: Treffer, Vorbeiflug und Start innerhalb eines Trefferkörpers.
- Schildabsorption, Hüllenschaden, Schiffszerstörung, Torpedoverbrauch und gelenkter Torpedotreffer. Zufallsbasierte Torpedoabwehr wird nur für den isolierten Flugbahn-Test ausgeschaltet.
- Alle drei Missionsarten bis zum Rückflug und Beginn der Landesequenz.
- Niederlage nach Verlust beider eskortierter Bomber.
- Abbremsen aus dem Reiseflug vor einem Navigationspunkt.
- Korrekte Zuordnung von linkem Trigger und rechter Schultertaste in der InputMap.
- Reale OpenGL-Darstellung im virtuellen X11-Display mit Mesa llvmpipe bei 1280 × 800: Menü, Briefing, Trägerstart und Cockpit. Die PNG-Dateien unter `docs/screenshots` stammen direkt aus der exportierten Anwendung.
- Zusätzlicher Grafik-Smoke-Test über 480 Frames einschließlich Übergang von Startsequenz zu Cockpitflug.

Die letzte Änderung nach dem Grafik-Smoke-Test betrifft das Abbremsen aus dem Reiseflug und die entsprechende Regressionprüfung; die Darstellung blieb unverändert.

## Beobachtungen und Grenzen

Beim Beenden meldet Godot eine `ObjectDB instances leaked at exit`-Warnung. Sie trat auch nach erfolgreichen Testläufen auf und ist für eine spätere Aufräumrunde zu untersuchen; es wurde kein Langzeit-Speicherprofil erstellt. Der virtuelle Grafiktreiber unterstützt keinen VSync-Wechsel. Beide Beobachtungen sind keine FPS-Messung auf dem Zielgerät.

Ein echtes Steam Deck, reale Controllerhardware, Steam-Gaming-Modus, ausführliche Spielbalance und lange Kampfsitzungen wurden nicht getestet. Das Ziel von 60 FPS ist damit noch nicht nachgewiesen. Der Prototyp verwendet vereinfachte prozedurale Modelle und Trefferkörper.

## Reproduzieren

Im Quellprojekt mit Godot 4.5 .NET und .NET 8 SDK:

```bash
dotnet build SpaceFleet.csproj
godot --headless --path . --quit-after 600 -- --self-test
```

Mit der exportierten Binary:

```bash
./SpaceFleet.x86_64 --headless --quit-after 600 -- --self-test
```

Erwartete Abschlussmeldung: `SELF TEST PASS: 3352 checks`.

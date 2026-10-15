# Flotte-A-Modelle

Originalgeometrie, MIT, Copyright (c) 2026 dark-man1978.

| Asset | Dreiecke | Materialflächen | Verwendung |
| --- | --- | --- | --- |
| kestrel_fleet_a.glb | 4.722 | 12 | Verbündete Kestrel sowie Spielerschiff bei Bombereskorte |
| cockpit_fleet_a.glb | 4.308 | 9 | Gemeinsames Cockpit aller Spielerklassen |

Ausrichtung: Vorwärts -Z, oben +Y. Einheiten: Meter. Cockpitgeometrie ist auf die 1280×800-Instrumente und vertikales FOV 80 abgestimmt; die Kamera kompensiert den Nachbrenner-FOV-Effekt durch Skalierung der X/Y-Achsen.

Die Dateien enthalten zusammengefasste Materialflächen mit Flächennormalen und sparsamen geometrischen Details. Das Materialmodell verwendet glTF-PBR mit korrekt linearisierten Grundfarben. Es sind keine Bildtexturen erforderlich. Die .import-Dateien enthalten die Godot-Ressourcen-UIDs und werden zusammen mit den Modellen versioniert.

Reproduktion mit numpy/scipy: `python3 Tools/build_fleet_assets.py` im Projektverzeichnis. Der Generator ist Entwicklungswerkzeug; er wird zum Spielen nicht benötigt.

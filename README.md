# DTP - Daten Transfer Programm

Ein in C# und Avalonia UI geschriebenes Automatisierungs-Tool zur schnellen und effizienten Generierung von Word- (.docx) und Excel-Dokumenten (.xlsx) aus ERP-Rohdaten. 

Das DTP nimmt unstrukturierte Listen (CSV/TXT), bündelt zusammengehörige Bauteile und Seriennummern zu logischen Auftrags-Sets und fügt diese vollautomatisch in vordefinierte Vorlagen ein.

[![DTP Vorschau](https://img.youtube.com/vi/lCIHfyXBnWg/maxresdefault.jpg)](https://youtu.be/lCIHfyXBnWg)

## Features (Version 1.1)

* **Intelligenter Import:** Drag & Drop für CSV/TXT-Dateien mit automatischer Set-Bildung und Duplikat-Filter.
* **Multi-File Export:** Gleichzeitiges Verarbeiten von hunderten Word- und Excel-Vorlagen.
* **Globale Dokumente:** Erstellung von projektübergreifenden Master-Übersichten (nur 1x pro Export-Lauf) mit summierten Daten.
* **Dynamische Zähler:** Automatische Berechnung von Platzhaltern wie `{{Gesamt_Auftraege}}` oder `{{Summe_Bauteilname}}`.
* **Unzerstörbarer Export (Sperrschutz):** Das Programm stürzt nicht ab, wenn Ziel- oder Vorlagendateien in Word/Excel geöffnet sind. Es nutzt Temp-Ordner-Kopien und automatische Zähler-Benennungen (`_1`, `_2`).
* **Dokumenten-Merge:** Automatisches Zusammenführen (Combine) von Einzeldokumenten zu einer großen Master-Datei über die Word-COM-Schnittstelle.

## Tech Stack

* **Framework:** .NET / C#
* **UI:** Avalonia UI (XAML)
* **MVVM-Pattern:** CommunityToolkit.Mvvm
* **Office-Schnittstellen:** MiniWord & MiniExcel
* **Datenverarbeitung:** CsvHelper

## Platzhalter-Handbuch (Syntax)

Das Programm ersetzt Platzhalter in Vorlagen (z.B. `{{Platzhalter}}`) automatisch. Leerzeichen und Sonderzeichen in Bauteilnamen werden intern zu Unterstrichen (z.B. `Kabel Mat` -> `Kabel_Mat`).

### 1. Normale Dokumente (Ein Bericht pro Auftrag)
* `{{Produkt}}` -> Die erste Seriennummer des Produkts im aktuellen Auftrag.
* `{{Produkt_1}}`, `{{Produkt_2}}` -> Die spezifische Seriennummer (z.B. für Tabellen).
* `{{Auftragsnummer}}` -> Lokale Metadaten des Auftrags.

### 2. Globale Dokumente (Eine Übersicht für ALLE Aufträge)
Setzt den Haken "Globales Dokument" in der Export-Ansicht voraus!
* **Zähler:** `{{Gesamt_Auftraege}}`, `{{Summe_Produktname}}`
* **Spezifische Metadaten:** `{{Auftrag_1_Datum}}`, `{{Auftrag_2_Tester}}`
* **Auftragsübergreifende Seriennummern:** Format: `{Bauteilname}_{Seriennummer-Position}_{Auftrags-Position}`
  * *Beispiel:* `{{Produkt_1_2}}` -> Holt die 1. Seriennummer des Teils 'Produkt' aus dem 2. Auftrag.

## Workflow

1. **Rohdaten (Step 1):** CSV/TXT per Drag & Drop laden (inkl. Sortierfunktion).
2. **Daten-Mapping (Step 2):** Spalten zuweisen (Seriennummern, Metadaten).
3. **Auftrags-Sets (Step 3):** Automatische Gruppierung prüfen und anpassen.
4. **Export (Step 4):** Word/Excel-Vorlagen einfügen (Check auf fehlende Platzhalter) und Export starten.

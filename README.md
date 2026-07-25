# Satoshi Ticker 1.0

Satoshi Ticker ist eine kleine Windows-Anwendung für den Desktop. Du trägst deinen Bitcoin-Bestand **in BTC** ein. Die Anwendung berechnet daraus automatisch den aktuellen Gegenwert in Euro und zeigt die tatsächliche Kursänderung der letzten zehn Minuten an.

Beispiel:

```text
128,42 € ▲ +0,37 %
```

## Funktionen

- manueller Bitcoin-Bestand in BTC
- automatischer Euro-Gegenwert
- tatsächliche 10-Minuten-Kursänderung auf Basis von 1-Minuten-Kerzen
- grüner Pfeil und grüner Prozentwert bei einem Anstieg
- roter Pfeil und roter Prozentwert bei einem Rückgang
- automatische Aktualisierung alle zehn Minuten
- manuelle Sofortaktualisierung
- Speicherung des BTC-Bestands ausschließlich lokal
- optionale Autostart-Funktion
- speichert die verschobene Fensterposition
- keine Wallet-Adresse, keine Seed Phrase und keine Ledger-Verbindung

## Voraussetzungen für die Entwicklung

- Windows 10 oder Windows 11, 64 Bit
- .NET 8 SDK
- Visual Studio Code
- VS-Code-Erweiterung **C# Dev Kit** von Microsoft

Prüfung:

```powershell
dotnet --version
```

Eine Ausgabe wie `8.0.423` ist geeignet.

## Projekt in VS Code starten

Öffne in VS Code genau den Ordner, in dem `SatoshiTicker.csproj` liegt. Öffne danach das integrierte Terminal:

```powershell
dotnet restore
dotnet run
```

Beim ersten Start erscheint die Eingabe für deinen BTC-Bestand. Beispiel:

```text
0,00128456
```

## Portable EXE erstellen

Die portable Variante enthält die benötigte .NET-Laufzeit. Sie kann auf einem anderen 64-Bit-Windows-PC gestartet werden, ohne dort zunächst .NET installieren zu müssen.

Im Projektordner:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Portable.ps1
```

Anschließend befindet sich die Datei hier:

```text
dist\portable\SatoshiTicker.exe
```

Diese EXE kannst du direkt starten oder in einen festen Ordner kopieren.

## Kleinere EXE erstellen

Diese Variante ist kleiner, benötigt auf dem Ziel-PC aber die .NET 8 Desktop Runtime:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Small.ps1
```

Ausgabe:

```text
dist\framework-dependent\SatoshiTicker.exe
```

## Richtigen Windows-Installer erstellen

Für einen klassischen Installationsassistenten wird **Inno Setup 6** benötigt.

1. Erstelle zunächst die portable EXE oder lasse das Skript dies automatisch erledigen.
2. Installiere Inno Setup 6.
3. Starte im Projektordner:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-Installer.ps1
```

Der fertige Installer liegt danach hier:

```text
dist\installer\SatoshiTicker-Setup-1.0.0.exe
```

Der Installer:

- installiert Satoshi Ticker unter den Benutzerprogrammen,
- legt einen Startmenü-Eintrag an,
- kann optional eine Desktop-Verknüpfung erzeugen,
- bietet eine normale Deinstallation über Windows.

## Bedienung

Mit einem Rechtsklick auf den Ticker öffnest du das Menü:

- **BTC-Bestand ändern**
- **Jetzt aktualisieren**
- **Mit Windows starten**
- **Beenden**

Mit gedrückter linker Maustaste kannst du das Fenster verschieben.

## Lokale Daten

Die Einstellungen werden hier gespeichert:

```text
%AppData%\SatoshiTicker\settings.json
```

Gespeichert werden nur:

- dein manuell eingetragener BTC-Bestand,
- die Autostart-Einstellung,
- die Fensterposition.

## Kursquelle

Die Anwendung verwendet die öffentliche Kraken-OHLC-Schnittstelle für BTC/EUR. Für die 10-Minuten-Tendenz wird der aktuelle Schlusskurs mit dem Schlusskurs von ungefähr zehn Minuten zuvor verglichen.

## Hinweis zur Prüfung

Der Quellcode wurde auf konsistente Namespaces, XAML-Ereignisse, Datei- und Netzwerkzugriffe, Eingabevalidierung sowie Veröffentlichungs- und Installationspfade überprüft. Eine Windows-Kompilierung muss auf einem Windows-System mit installiertem .NET 8 SDK erfolgen.

# KubSMTP

Native SMTP-Testanwendung für Windows, C#/.NET 10 und WinUI 3. Aktuelle Quellcode-Version: **v.1.0.16**.

## Starten

Die portable x64-Ausgabe liegt als **einzelne Datei** unter `artifacts/KubSMTP.App.exe`. Nur diese EXE kopieren und starten; .NET, WinUI, Bibliotheken und Ressourcen sind enthalten. Eine separate Runtime-Installation ist nicht erforderlich. Seit v.1.0.9 zeigt ein nativer Starter beim ersten Start ein Fortschrittsfenster mit Prozentanzeige und Abbrechen-Schaltfläche. Er entpackt die Programmdateien und startet KubSMTP direkt aus dem vollständigen Cache. Weitere Starts verwenden diesen Cache ohne erneutes Entpacken und ohne Fortschrittsfenster. Nach einem Update mit geändertem Inhalt oder dem Entfernen des Caches wird erneut entpackt. Es gibt keinen Installer und es sind keine Administratorrechte nötig. Die äußere EXE darf auch umbenannt werden.

Dieselbe portable EXE liegt auch unter `artifacts/KubSMTP-win-x64/KubSMTP.App.exe` und `artifacts/KubSMTP-win-x64-v1.0.16/KubSMTP.App.exe`. Zusätzliche Dateien aus älteren Veröffentlichungen in diesen Ordnern werden für die Einzeldatei-Ausgabe nicht benötigt. Bei Ausführungsbeschränkungen auf Netzwerkfreigaben nur die EXE lokal kopieren. Windows 11 x64 ist die primäre Zielplattform.

## Verwendung

Servername oder IPv4/IPv6, Port und Verschlüsselung angeben. 587/STARTTLS und 465/implizites TLS sind typische Kombinationen, 25/2525 und eigene Ports sind möglich. „Verbindung testen“ sendet keine Nachricht. Anmeldung ohne TLS ist aus Sicherheitsgründen gesperrt; für unverschlüsselte Relay-Tests „Keine Anmeldung“ wählen. Zertifikate werden überprüft, Zertifikatsfehler verhindern die TLS-Verbindung jedoch nicht. Das Liveprotokoll zeigt orange Warnungen mit den erkannten Ursachen: etwa abgelaufen (mit Ablaufdatum), noch nicht gültig, abweichender Hostname, nicht vertrauenswürdiger Aussteller, unvollständige Zertifikatskette oder Widerrufsfehler. Mehrere Ursachen werden zusammen aufgeführt und auch in der Logdatei gespeichert. Dies gilt für implizites TLS sowie verpflichtendes und optionales STARTTLS.

Die Portauswahl bietet 25, 465, 587 und 2525 sowie „Benutzerdefiniert“ für einen eigenen Port von 1 bis 65535. Portauswahl und Verschlüsselungsmodus sind unabhängig; gespeicherte eigene Ports werden beim Laden des Serverprofils übernommen.

Absender, Empfänger, CC/BCC, Betreff und Text oder HTML eingeben, optional mehrere Dateien anhängen und „Testmail senden“ verwenden. Beide Nachrichtenformate enthalten eine bearbeitbare Testmail-Vorlage mit Hinweis auf KubSMTP. Beim Formatwechsel bleibt der zuletzt bearbeitete Inhalt je Format während der Sitzung erhalten. Direkt neben dem Nachrichtenformat lässt sich die Codierung auswählen: UTF-8 (Standard), ISO-8859-1, Windows-1252 oder US-ASCII. Sie wird für Nachrichtentext und Betreff verwendet und im Entwurf gespeichert; alte Entwürfe verwenden UTF-8. Nicht darstellbare Zeichen führen zu einer verständlichen Fehlermeldung statt stiller Ersetzung. Adresslisten akzeptieren Komma und Semikolon. Nach einem Versandversuch erscheint ein Popup mit Erfolg, Fehler oder Abbruch. „Vom SMTP-Server angenommen“ bestätigt keine Zustellung ins Postfach. Nach Timeout/Abbruch kann der Versandstatus unbekannt sein; es gibt keine automatische Wiederholung.

Das linke Formular verwendet seit v.1.0.11 einen nativen WinUI-ScrollViewer. Damit verwenden Formular und interne Scrollbereiche der Eingabefelder denselben Scrolltyp; Mausradbewegungen über einzeiligen Feldern werden an das Formular weitergegeben. Der Nachrichteneditor scrollt längere Inhalte selbst und reicht Scrollbewegungen an seinen Grenzen an das Formular weiter. Maus-, Touchpad-, Touch- und Tastatureingabe bleiben nativ; eigene Mausrad-Handler sind nicht erforderlich. Das Liveprotokoll lässt sich rechts ein-/ausblenden, pausieren, kopieren und exportieren. Zugangsdaten und MIME-Inhalte werden vor UI- und Dateiausgabe ausgeblendet. Export kopiert die sichtbaren, begrenzten Zeilen; vollständige Sitzungsprotokolle liegen unter `Logs`. Serverantworten bleiben im Original.

Die Oberfläche einschließlich Versand-Popup verwendet die Windows-/WinUI-Designressourcen. Neutrale Flächen, Eingabefelder, Rahmen, Textfarben und Akzentbuttons folgen dem hellen bzw. dunklen Windows-App-Modus. Änderungen der Systemeinstellung werden während der Laufzeit über `UISettings.ColorValuesChanged` auf den UI-Thread übernommen; ein Neustart ist nicht erforderlich. Das Liveprotokoll verwendet unabhängig vom Systemmodus eine dunkle Terminaldarstellung. Seit v.1.0.16 sind Client-Zeilen hellcyan, Server-Zeilen hellgelb, Hinweise grün, Warnungen orange und Fehler hellrot.

Die Windows-Anzeigesprache bestimmt Deutsch (`de-*`) oder Englisch als Fallback. Es gibt keinen Sprachumschalter. Änderungen der Windows-Sprache werden beim nächsten Programmstart berücksichtigt.

## Daten

Profile, Entwürfe, Protokolle und Exporte liegen unter `%LOCALAPPDATA%\KubSMTP`; der Programm- und Runtime-Cache liegt unter `%LOCALAPPDATA%\KubSMTP\Runtime\<Inhaltshash>`. Serverprofile werden ausdrücklich gespeichert; Mailentwürfe nur über „Entwurf speichern“. Entwürfe enthalten Adressen, Body und ursprüngliche Dateipfade, keine Kopien der Anhänge. Zugangsdaten bleiben standardmäßig im Speicher; optional werden sie beim Speichern des Serverprofils per DPAPI für den aktuellen Windows-Benutzer verschlüsselt abgelegt. Logs können Empfängeradressen enthalten. Aufbewahrungsgrenzen: 14 Tage bzw. 50 MB, Bereinigung beim Start und nach Tests.

## Entwickeln

Voraussetzungen zum Entwickeln: Windows, .NET-10-SDK, Python 3 (Standardbibliothek), Zig 0.17.1 und Internetzugang für den ersten NuGet-Restore. WinUI/XAML-Buildtools werden über Windows-App-SDK-Pakete referenziert. Zig und Python werden nur zum Erstellen der portablen EXE benötigt. `Publish.ps1` akzeptiert abweichende Werkzeugpfade über `-Dotnet`, `-Zig` und `-Python`.

```powershell
dotnet build KubSMTP.sln -c Release
dotnet run --project tests/KubSMTP.Tests -c Release
./build/Publish.ps1
./build/Test-Portable.ps1
```

Der Testläufer verwendet ausschließlich einen lokalen SMTP-Server und künstliche `.test`-Adressen. Er prüft Versionsübergänge, MIME/BCC, Geheimnisfilter, unverschlüsselten/opportunistischen SMTP-Versand, fehlendes verpflichtendes STARTTLS sowie DPAPI-Speicherung. Zusätzlich prüfen 30 TLS-Fälle selbstsignierte, abgelaufene, noch nicht gültige und zum Hostnamen unpassende Zertifikate sowie kombinierte Fehler: jeweils mit implizitem TLS, verpflichtendem und optionalem STARTTLS auf Deutsch und Englisch. Geprüft werden erfolgreicher Verbindungsaufbau, Anmeldung und Versand trotz Zertifikatsfehlern, genaue Warnungen in Live- und Dateiprotokoll sowie weiterhin maskierte Zugangsdaten und Nachrichteninhalte. MIME-Serialisierung und erneutes Einlesen prüfen alle vier Codierungen mit Klartext und HTML einschließlich Sonderzeichen in Betreff und Body. Weitere Prüfungen sichern die Ablehnung nicht darstellbarer Zeichen und die Entwurfskompatibilität.

Die Oberfläche wurde direkt im Windows-Fenster geprüft: Standardports und eigener Port, HTML-Vorlage, Erfolgspopup nach lokaler SMTP-Annahme und Fehlerpopup bei SMTP-Ablehnung. Für v.1.0.5 wurde die native Scrollansicht mit Mausradeingabe über Textfeldern vor und nach einer Größenänderung geprüft. Ein automatisierter Hell-/Dunkelwechsel im selben laufenden Fenster wurde einschließlich gerenderter Aufnahmen in kompakter Ansicht kontrolliert.

`Publish.ps1` veröffentlicht die selbstenthaltende WinUI-Anwendung mit `PortablePayload=true` zunächst als Verzeichnis ohne .NET-Einzeldatei-Extraktion. `Build-Bootstrap.ps1` erstellt den nativen Win32-Starter; `Pack-Portable.py` hängt ein Archiv mit blockweise komprimierten Dateien an. Die Auslieferung bleibt eine EXE. Der Starter benötigt nur Windows-System-DLLs, zeigt den Fortschritt anhand tatsächlich geschriebener Bytes und prüft jede Datei per SHA-256. Erst nach vollständigem Erfolg wird der Cache als startfähig markiert; parallele Starts werden über einen Mutex koordiniert. `Test-Portable.ps1` testet eine umbenannte EXE bei leeren .NET-Suchpfaden, Fortschrittsereignisse, den späteren Cache-Start und beide UI-Designs. Eine zweite versteckte .NET-Einzeldatei-Extraktion ist ausgeschlossen. Berichte: `artifacts/portable-check-v*.json`.

Versionsanhebung nach einer abgeschlossenen Folgeiteration: `./build/Next-Version.ps1`. Auf 1.0.100 folgt 1.1.0. Wiederholungsbuilds verändern die Version nicht.

## Prüfgrenzen

Build und lokale SMTP-Integration wurden automatisiert geprüft. Die portable Einzeldatei wurde ohne zusätzliche Programmdateien und mit leeren .NET-Suchpfaden gestartet; die Host-Diagnose bestätigt die selbstenthaltende Anwendung im eigenen Cache. Die XAML-Oberfläche wurde zusätzlich anhand echter RenderTargetBitmap-Aufnahmen in heller und dunkler Darstellung bei 1440 bzw. 1040 Pixel Fensterbreite visuell geprüft; auch der nach unten gescrollte Mail-Editor wurde gesichtet. Physische Touchpad-Hardware und das Umschalten der globalen Windows-Einstellung wurden in dieser Sitzung nicht bedient. Produktive SMTP-Anbieter, erfolgreiche TLS-Verbindungen mit öffentlichen Zertifikaten, OAuth2-Provider, ARM64, MSIX-Signierung und Screenreader benötigen gesonderte Abnahme. OAuth2 akzeptiert ein bereitgestelltes Zugriffstoken; eine Provider-Kontoanmeldung ist nicht eingebaut. Die Ausgabe ist eine selbstenthaltende portable EXE, kein signierter Installer.


## Oberfläche v.1.0.1

Die native XAML-Oberfläche übernimmt Karten, Feldaufteilung, Typografie, abgerundete Eingaben und den blauen Sendebutton des Mockups. Das Terminal verwendet die dunkelblaue Farbpalette des ursprünglichen Mockups. Client-Befehle sind cyan, Serverantworten hellgrün, Hinweise blaugrau und Fehler orange. Systemdarstellung und Windows-Anzeigesprache werden beim Start berücksichtigt. Zusätzliche Profiloptionen sind eingeklappt; Entwurfsaktionen liegen im Menü. Die Anwendungs-PRI wird jetzt durch die Windows-SDK-Buildtools mit erzeugt und veröffentlicht.

Ein Entwicklungsstart mit --capture-ui speichert eine Aufnahme der eigenen XAML-Ansicht unter %LOCALAPPDATA%\KubSMTP\Exports und beendet das Vorschaufenster; --capture-dark, --capture-compact und --capture-bottom ergänzen die Ansicht. --capture-theme-cycle wechselt im selben Fenster zwischen beiden Designs, prüft ActualTheme und speichert eine zusätzliche Aufnahme. Diese Optionen verändern keine Profile und versenden keine Nachricht.


## Startoptimierung v.1.0.8

Die App referenziert die benötigten WinUI-/DWrite-Komponenten gezielt. Die ungenutzten ONNX- und DirectML-Inferenzbibliotheken werden über den SDK-Ausschlussmechanismus nicht mit ausgeliefert. Die portable Einzeldatei bleibt komprimiert und enthält weiterhin ihre benötigte Runtime. Unkomprimierte Veröffentlichung und ReadyToRun wurden im Startvergleich verworfen, da sie den ersten Start hier verschlechterten. Der erste Start nach einem Update benötigt einen neuen Entpack-Cache; spätere Starts verwenden ihn erneut.

Messung: ./build/Measure-Startup.ps1 -Executable artifacts/KubSMTP.App.exe -Label after. Gemessen wird ab Prozessstart bis zum ersten sichtbaren Hauptfenster in einer lokalen Kopie, zuerst mit leerem, dann mit wiederverwendetem Entpack-Cache. Kopierzeit, Netzwerkstart und die absichtliche Wartezeit der UI-Aufnahme sind nicht Teil der Fensterzeit. Ergebnisse liegen unter artifacts/startup-*.json; es sind keine garantierten Startzeiten auf anderen Rechnern.

Ergebnis dieser Sitzung: Entpackter Umfang rund 206 → 167 MB, EXE rund 94 → 78 MB. Weitere Starts mit vorhandenem Cache lagen bei rund 0,7 Sekunden bis zum sichtbaren Fenster. Die ersten Starts mit neuem Cache schwankten stark; gegenüber der Ausgangsversion konnte kein verlässlicher Zeitgewinn nachgewiesen werden. Für den Betrieb die EXE lokal ablegen; Netzwerkübertragung und Sicherheitsprüfung können zusätzlich Zeit benötigen.

## Fortschrittsfenster v.1.0.9

Der native Fortschrittsdialog und sein Abbrechen-Button wurden im Windows-Fenster geprüft. Ein Abbruch lieferte Exitcode 1223 und keinen vollständigen Cache. Der nächste Start baute die unvollständigen Dateien erfolgreich neu auf. Der vollständige portable Test prüft echte Zwischenwerte bis 100 %, erfolgreiche UI-Aufnahmen und einen Folgestart ohne erneutes Entpacken oder Fortschrittsfenster.

Der Starter wird mit [Zig 0.17.1](https://ziglang.org/download/) gebaut. Für `zig-x86_64-windows-0.17.1.zip` wurde SHA-256 `b03f477a1943673e02aca0f570e8030dffd536ce1f45cbc4d3326ec31159562e` überprüft. Windows Cabinet/XPRESS-Huffman übernimmt die Kompression und Dekompression; es werden keine zusätzlichen Werkzeuge auf dem Zielrechner gestartet oder installiert.

Für isolierte Entwicklertests dienen `KUBSMTP_PORTABLE_CACHE` und `KUBSMTP_PORTABLE_TRACE`. Nur bei aktivierter Diagnose kann `KUBSMTP_PORTABLE_UI_TEST_DELAY_MS` die Extraktion für UI-Tests verzögern (maximal 100 ms je Block); im normalen Betrieb gibt es keine künstliche Verzögerung. `Measure-Startup.ps1` unterscheidet jetzt `FeedbackMs` (erstes sichtbares Fenster, ggf. Fortschritt) und `WindowMs` (eigentliches KubSMTP-Fenster).

## Startdialog v.1.0.10

Der native Startdialog folgt der bereitgestellten KubDHCP-Vorlage mit KubSMTP-Namen und -Icon: dunkler Hintergrund, große Überschrift, separate Statuszeile mit Prozentwert und flacher türkisfarbener Fortschrittsbalken. Das Schließen-Symbol oben rechts ersetzt den bisherigen zusätzlichen Abbrechen-Button. Der Dialog skaliert mit der Bildschirm-DPI, verwendet einen abgerundeten Windows-Fensterrahmen und benötigt weiterhin ausschließlich Windows-Systembibliotheken. Deutsch und Englisch werden weiterhin anhand der Windows-Anzeigesprache gewählt.

Die Darstellung und der Abbruch über das Schließen-Symbol wurden im echten Windows-Fenster geprüft. Der Abbruch hinterließ keinen vollständigen Cache. Der portable Test bestätigt weiterhin Fortschritts-Zwischenwerte, einen Start ohne installierte Runtime, den Folgestart aus dem Cache sowie die unveränderte Hauptoberfläche in beiden Designs.

## Scrollkorrektur v.1.0.11

Das Formular verwendet jetzt ScrollViewer statt ScrollView, passend zu den internen ScrollViewern der Textfelder. Die Aufnahmeoption --capture-bottom wurde an den neuen Steuerelementtyp angepasst. Im Windows-Fenster wurde das Scrollen über Server- und Absenderfeld in beide Richtungen geprüft. Release-Build, vorhandener Testläufer und portabler Kalt-/Warmstart einschließlich beider Designs waren erfolgreich. Physische Touchpad-Eingabe wurde nicht geprüft.

## Kompaktes Formular v.1.0.12

Links stehen Verschlüsselung und Anmeldung sowie Absender und Empfänger jeweils in zwei gleich breiten Spalten. Betreff und Nachrichtenformat/Codierung teilen sich eine weitere Zeile; der mehrzeilige Nachrichteneditor bleibt über die volle Breite. Anhang- und Entwurfsbutton stehen nebeneinander. Die abschließende Zeile zeigt links Anhangszahl und Status und rechts den Sendebutton. Der zusätzliche Trennabstand vor dem Sendebutton entfällt. Dadurch entfallen bei unveränderten Feldhöhen drei bisher separate Formularzeilen und zusätzlicher Trennabstand.

## Gemeinsame Verbindungsaktionen v.1.0.13

Der aufklappbare Bereich für Serverprofil und Zugangsdaten steht links neben dem Verbindungstest. Der Sitzungshinweis befindet sich im aufgeklappten Profilbereich. Der Verbindungstest bleibt beim Aufklappen oben in derselben Zeile; im eingeklappten Zustand entfällt eine weitere Formularzeile.

## Terminaldarstellung v.1.0.14

Das Liveprotokoll folgt der Bildvorlage mit fast schwarzem, leicht grünem Hintergrund (#0C1511), grünem Rahmen (#334C3E), Monospace-Titel, grüner Statusanzeige mit Wiedergabesymbol und grünen Protokollzeilen (#86E9A8). Zeitstempel stehen in eckigen Klammern; Warnungen und Fehler bleiben orange erkennbar. Die bisherigen Zwischenlinien und die separate Client-/Server-Legendenzeile entfallen; die Legende steht im Untertitel. Kopieren, Export, Leeren, Autoscroll und Pause bleiben am unteren Rand verfügbar. Die Trennlinien unter der Titel- und über der Statusleiste verwenden im dunklen Design den aus der zweiten Vorlage übernommenen Grauwert #323232; im hellen Design #D0D0D0.

Release-Build und portabler Kalt-/Warmstart wurden erfolgreich geprüft. Die kompakte Hauptansicht wurde nach einem Designwechsel im selben Fenster hell und dunkel gerendert und visuell kontrolliert: gemeinsame Profil-/Testzeile, Terminaldarstellung und beide grauen Fenstertrennlinien.

## Kompakter Fensterstart v.1.0.15

Beim Start bleibt das Liveprotokoll ausgeblendet. Das Fenster beginnt mit 700 logischen Pixeln Breite; seine Höhe folgt dem Formularinhalt. Der Liveprotokoll-Button erweitert das Fenster um einen gleich breiten rechten Bereich und verkleinert es beim Ausblenden wieder auf die Formularbreite. Beide Bereiche enden auf gleicher Höhe. Die Arbeitsbereichshöhe ist durch den Formularinhalt begrenzt. Größen berücksichtigen die DPI-Skalierung und den verfügbaren Monitor-Arbeitsbereich; auf kleineren Bildschirmen bleibt das Formular scrollbar.

--capture-layout-cycle prüft den kompakten Start, die Vergrößerung, gleiche Breite/Höhe der beiden Bereiche, fehlenden Leerraum unter dem Formular und die Rückkehr zur Ausgangsgröße. Aufnahmen UI-layout-start.png / UI-layout-log.png und layout-check.json entstehen im Exportordner. Der portable Kalt-/Warmstarttest führt diesen Zyklus ebenfalls aus. --capture-log zeigt das Protokoll für gezielte Aufnahmen an.

## Unterscheidbare Protokollfarben v.1.0.16

Client-Zeilen (C) erscheinen hellcyan (#67D8FF), Server-Zeilen (S) hellgelb (#F3DE8A). Kennbuchstabe und Nachricht verwenden jeweils dieselbe Rollenfarbe; Zeitstempel sind dezent graugrün. Hinweise bleiben grün, Warnungen orange, Fehler hellrot. Alle Textfarben heben sich deutlich vom Hintergrund #0C1511 ab. Die Kombination --capture-ui --capture-log --capture-log-sample rendert ausschließlich für die Entwicklung fiktive C-/S-/I-/W-/E-Zeilen, ohne SMTP-Verbindung oder Mailversand.

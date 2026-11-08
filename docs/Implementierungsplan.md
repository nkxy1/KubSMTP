# KubSMTP – Implementierungsplan

Stand: 07.11.2026 · erste geplante Programmversion: **v.1.0.0**

Die erste native Implementierung v.1.0.0 liegt in src/ vor, die veröffentlichte x64-Ausgabe unter artifacts/. Stand und Prüfgrenzen sind in README.md dokumentiert. Das frühere GUI-Mockup bleibt eine Simulation ohne echten Versand.

## 1. Grundlage und Architektur

C# mit .NET 10, WinUI 3/XAML und einer zum Implementierungsbeginn geprüften stabilen Windows-App-SDK-Version. Paketversionen zentral festschreiben. Windows 11 primär testen, Windows 10 entsprechend der Unterstützung der gewählten SDK-/Runtime-Version; x64 zuerst.

MVVM mit CommunityToolkit.Mvvm, MailKit für SMTP und MimeKit für Nachrichten und Anhänge. UI, Netzwerk, Konfiguration und Protokollierung trennen. Netzwerkoperationen asynchron mit Timeout und CancellationToken ausführen.

```text
src/KubSMTP.App/          WinUI-Ansichten, ViewModels, Ressourcen
src/KubSMTP.Core/         Optionen, Nachrichtenmodell, Ergebnisse
src/KubSMTP.Services/     SMTP, MIME, Protokollierung, Profile
tests/KubSMTP.Tests/      Kern- und lokale SMTP-Integrationstests
build/                   Versions- und Veröffentlichungsworkflow
version.json             Zentrale Produktversion
docs/                    Planung und Designreferenz
assets/                  Icon und Windows-Assets
```

Ergebnis: Startfähiges Fenster mit lokalisierten Beschriftungen, Icon und Versionsanzeige unten rechts.

## 2. SMTP-Verbindung und Anmeldung

Serverfeld für DNS-Namen, IPv4 und IPv6; Port frei zwischen 1 und 65535. Vorschläge: 25, 465, 587 und 2525. Port 2525 ist ein verbreiteter alternativer Anbieterport, kein allgemeiner Standard. Port und Verschlüsselung unabhängig editierbar lassen.

| Auswahl | MailKit-Modus | Verhalten |
| --- | --- | --- |
| Unverschlüsselt | None | SMTP ohne TLS |
| STARTTLS erforderlich | StartTls | Abbruch, wenn STARTTLS fehlt oder TLS scheitert |
| STARTTLS falls verfügbar | StartTlsWhenAvailable | Unverschlüsselte Verbindung möglich; tatsächlichen Modus anzeigen |
| TLS ab Verbindungsbeginn | SslOnConnect | TLS beim Verbindungsaufbau, üblich auf 465 |

Standardvorschlag: 587 und STARTTLS erforderlich. Presets nur nach bewusster Auswahl anwenden. „SSL/TLS“ bezeichnet implizites TLS; veraltetes SSL wird nicht zusätzlich angeboten. TLS-Version und Cipher gemäß Betriebssystemfähigkeiten aushandeln, normalerweise TLS 1.2/1.3. Keine Zusage für beliebige historische Verfahren.

Anmeldung: keine für Relay-Tests, Benutzername/Passwort mit von Server und MailKit unterstützten SASL-Verfahren oder OAuth2 mit bereitgestelltem Zugriffstoken. Automatische Microsoft-/Google-Kontoanmeldung wäre eine Erweiterung mit eigener App-Registrierung. Geheimnisse standardmäßig nur im Speicher halten; optional mit Windows-DPAPI benutzergebunden geschützt speichern.

„Verbindung testen“ prüft DNS, TCP, Begrüßung, EHLO, gegebenenfalls TLS und Anmeldung, ohne Mailversand. HELO-Fallback für einfache unverschlüsselte Server berücksichtigen. Seit v.1.0.3 werden Zertifikatsketten, Gültigkeitszeiträume und Hostnamen geprüft, Zertifikatsfehler aber akzeptiert. Die TLS-Verbindung wird fortgesetzt und alle erkannten Gründe als lokalisierte orange Warnungen im Liveprotokoll sowie in der Logdatei ausgegeben. Ablaufdatum, noch nicht gültige Zertifikate, Hostnamenfehler und Vertrauens-/Kettenfehler werden unterschieden.

## 3. Nachrichteneditor und Versand

- Absender, Empfänger, CC, BCC, Betreff und mehrzeiliger Body. Mehrere Adressen mit Komma/Semikolon und Anzeigenamen unterstützen.
- Plaintext und optional HTML, UTF-8 für internationale Inhalte.
- Eine oder mehrere Dateien über Windows-Dateiauswahl anhängen; Name, Größe und Entfernen-Aktion anzeigen. Originaldateien nicht automatisch kopieren.
- Host, Port, Absender, Empfänger, Adresssyntax und Lesbarkeit der Anhänge vor Versand prüfen. Server-SIZE-Limit und MIME-Größenzuschlag berücksichtigen.
- Mit MimeKit MIME-Nachrichten erzeugen. BCC-Adressen in die explizite SMTP-Envelope-Empfängerliste aufnehmen und aus sichtbaren Nachrichtenheadern entfernen.
- „Testmail senden“ verbindet, authentifiziert falls gewählt, sendet, protokolliert und trennt. Doppelklickversand verhindern; Abbruch anbieten und Änderungen während des Versands gegen inkonsistente Zustände schützen.
- „Vom SMTP-Server angenommen“ als Ergebnis anzeigen: SMTP-Annahme beweist keine Zustellung ins Postfach. Nach Abbruch während DATA kann das Ergebnis unbekannt sein; nicht automatisch erneut senden.

## 4. WinUI-Oberfläche und Liveprotokoll

Fluent-Oberfläche, Segoe UI, Windows-Standardressourcen für helle und dunkle Darstellung. Links Verbindung und Mail in einem nativen WinUI-ScrollView für Maus, Touchpad und Touch, rechts ein Monospace-Liveprotokoll mit der ursprünglichen dunkelblauen Farbpalette (seit v.1.0.7 wieder unabhängig vom Systemmodus). Änderungen des Windows-App-Modus werden seit v.1.0.5 während der Laufzeit übernommen. Startgröße etwa 1440 × 960 logische Pixel, größenverstellbare Spalten. Bei kleinen Fenstern den rechten Bereich separat aufrufbar machen.

- „Liveprotokoll“ blendet rechts ein/aus. Die Aufzeichnung läuft während des Tests unabhängig von der Sichtbarkeit.
- Zeitstempel, Richtung, SMTP-Befehle/-Antworten, Phasen und TLS-Ergebnis anzeigen. SMTP-Rohtexte unverändert lassen; eigene Meldungen lokalisieren.
- MailKit-Protokolladapter führt UI und bereinigte Logdatei aus derselben Ereignisquelle zusammen. Fragmente inkrementell dekodieren und zeilenweise puffern.
- RedactSecrets aktivieren; Passwort, Token und SASL-Nutzdaten vor jeder Ausgabe maskieren. Body und Anhangbytes standardmäßig zusammenfassen.
- UI-Updates bündeln, Zeilenanzahl im Speicher begrenzen und Logrotation umsetzen. Autoscroll, Pause der Anzeige, Kopieren, Leeren und Textexport. Richtung und Text ergänzen die Farbkennzeichnung; keine Terminalbefehlseingabe.

## 5. Benutzerdaten und Lokalisierung

Alle erzeugten Laufzeitdateien unter `%LOCALAPPDATA%\KubSMTP\` im jeweiligen Windows-Benutzerprofil ablegen; den Pfad mit `Environment.GetFolderPath(LocalApplicationData)` ermitteln. Keine Laufzeitdateien neben der EXE und keine Administratorrechte für den Normalbetrieb.

```text
%LOCALAPPDATA%\KubSMTP\
  settings.json           Darstellung, Fensterposition, Sprache
  profiles.json           Serverprofile ohne Klartextgeheimnisse
  Secrets/                Optional: benutzergebunden geschützte Geheimnisse
  Logs/                   Bereinigte SMTP-Sitzungsprotokolle
  Drafts/                 Nur ausdrücklich gespeicherte Entwürfe
  Exports/                Standardziel für gespeicherte Exporte
```

Atomare JSON-Schreibvorgänge und Schema-Version für Migrationen. Nachrichteninhalt und Empfänger nicht ungefragt als Entwurf speichern. Logs können Adressdaten enthalten: Löschfunktion und begrenzte Aufbewahrung vorsehen, vorgeschlagen 14 Tage beziehungsweise 50 MB insgesamt.

Ressourcen `de-DE` und `en-US`. Erste passende Windows-Anzeigesprache mit Präfix `de` → Deutsch, sonst Englisch. Keine manuelle Sprachumschaltung; ausschließlich die Windows-Anzeigesprache bestimmt die Sprache. Validierung, Tooltips, Status und eigene Fehlertexte ebenfalls übersetzen; SMTP-Rohantworten unverändert lassen.

## 6. Icon und Versionierung

Icon-Entwurf: dunkelblaue abgerundete Kachel, türkisblaues isometrisches K, Briefumschlag und limettengrüner Sendepfeil im Stil der KubDHCP-Vorlage. Anschließend Windows-Assetgrößen und mehrstufiges ICO erzeugen; Lesbarkeit bei 16, 24, 32, 48 und 256 Pixeln prüfen.

Zentrale Produktversion zunächst `1.0.0`, Anzeige exakt **v.1.0.0** unten rechts. Jede abgeschlossene Implementierungsiteration erhöht einmal; Wiederholungsbuilds erhöhen nicht. Planung und Mockup sind noch keine veröffentlichte Programmiteration.

Regel: Patch < 100 → Patch + 1; Patch = 100 → Minor + 1, Patch = 0. Beispiel: `v.1.0.99` → `v.1.0.100` → `v.1.1.0` → `v.1.1.1`. Major nur bewusst ändern. Ein Versionsskript und eine zentrale Datei speisen UI, Projektmetadaten und Paketversion. MSIX-Version vierteilig, zunächst `1.0.0.0`.

## 7. Qualität und Auslieferung

1. Kernprüfungen: Versionsübergang 99/100, Adresslisten, BCC-Header, MIME-Anhänge, Geheimnisfilter und Datenpfade.
2. Kontrollierter SMTP-Testserver: ohne Anmeldung, korrekte/falsche Anmeldung, implizites TLS, verpflichtendes/optionales/fehlendes STARTTLS, Zertifikatsfehler, IPv4/IPv6, Timeout und Abbruch.
3. Versandfehler: einzelne abgelehnte Empfänger, Relay-Verbot, Größenlimit, 4xx/5xx und Abbruch während DATA. Teilannahme je Empfänger korrekt melden.
4. UI: Deutsch/Englisch/Fallback, hell/dunkel, 100–200 % Skalierung, lange Werte, Tastatur, Screenreader und viele Logzeilen.
5. Speicherung: getrennte Windows-Benutzer, beschädigte Einstellungen, nicht beschreibbarer Datenpfad, fehlender Anhang und Logrotation.
6. Lokal startbares x64-Paket, dann Release-Paket mit dokumentierten Voraussetzungen. MSIX-Signierung und Verteilung für das gewünschte Umfeld festlegen.

Abnahme: Alle Eingabefelder und Anhänge funktionieren; gängige Port-/TLS-Kombinationen sind testbar; Logs erscheinen rechts ohne Zugangsdaten; Laufzeitdateien liegen im Benutzerprofil; Sprache und Versionsübergang entsprechen den Vorgaben.

## Quellen

- [Microsoft: WinUI 3](https://learn.microsoft.com/windows/apps/winui)
- [Microsoft: Erste WinUI-Anwendung](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/get-started-winui3-for-desktop)
- [MailKit: SecureSocketOptions](https://mimekit.net/docs/html/T_MailKit_Security_SecureSocketOptions.htm)
- [MailKit: Protokollierung und Maskierung](https://mimekit.net/docs/html/Properties_T_MailKit_ProtocolLogger.htm)



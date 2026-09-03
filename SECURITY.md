# Sicherheit

## Schwachstellen melden

Sicherheitsrelevante Funde bitte **nicht** als öffentliches Issue anlegen, sondern über
[managentis.com/kontakt](https://managentis.com/kontakt/) melden. Eine Rückmeldung erfolgt,
sobald es der Projektbetrieb zulässt — eine feste Reaktionszeit wird nicht zugesagt.

## Unterstützte Versionen

Gepflegt wird ausschließlich der aktuelle Stand des `main`-Branch. Für ältere Stände gibt es
keine Rückportierungen. Die Programmdateien werden nicht signiert; wer eine Datei aus einer
anderen Quelle als diesem Repository erhält, sollte ihr nicht vertrauen.

## Bekannte Rahmenbedingungen

Diese Punkte sind keine Schwachstellen, sondern bewusste Eigenschaften. Sie stehen hier, damit
sie vor dem Einsatz bekannt sind.

- **LumenHut lädt fremde Webseiten in echte Browser.** Jede Messung führt den Code der Zielseite
  und aller von ihr eingebundenen Dritten aus. Playwright verwendet je Lauf einen frischen,
  nicht persistenten Kontext, aber die Zielseite sieht die IP-Adresse des messenden Rechners.
- **Nur abgemeldete URLs oder Testkonten messen.** Eine angemeldete Ansicht lädt echte Daten in
  den Browser und schreibt deren URL und Fehlermeldungen in die lokale Datenbank.
- **Die Datenbank ist nicht verschlüsselt.** Sie liegt im Anwendungsdatenordner des Benutzers,
  der auf diesen beschränkt ist (0700 unter macOS und Linux, LocalAppData-ACL unter Windows).
  Ein Backup- oder Sync-Programm nimmt sie trotzdem mit.
- **URLs werden vor dem Speichern reduziert:** Zugangsdaten (`https://benutzer:passwort@host/`)
  immer, der Query-String ebenfalls, solange nicht ausdrücklich anders eingestellt. Query-Strings
  tragen regelmäßig Sitzungs-, Reset- und Einladungs-Tokens.
- **Das Proxy-Passwort wird nicht gespeichert.** Es gilt nur für die laufende Sitzung und steht
  nicht in `settings.json`. Im Protokoll werden Proxy-Zugangsdaten an der Quelle maskiert, und
  das SQL-Logging von EF Core bleibt aus, weil dessen Parameter die URLs enthalten würden.
- **Exporte prüfen, bevor sie weitergegeben werden.** Fehlermeldungen der Engines werden gekürzt
  und von Zugangsdaten befreit, können aber weiterhin interne Hostnamen, Ports und Pfade
  enthalten.
- **Beim ersten Start lädt Playwright die Browser-Engines** von `cdn.playwright.dev` und
  `playwright.download.prss.microsoft.com`. Danach arbeitet LumenHut selbst ohne Verbindung nach
  außen, abgesehen von den gemessenen Seiten.

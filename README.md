# Gemini GUI - Native Windows Client für Google Gemini 2.5 Pro

Ein hochperformanter nativer Windows-Desktop-Client für Google Gemini 2.5 Pro, entwickelt mit WPF und .NET 8.

## Features

### 🚀 Performance
- **Native Windows-Anwendung** mit WPF für optimale Performance
- **Lokale SQLite-Datenbank** für persistente Chat-Speicherung
- **Virtualisierung** für große Chat-Verläufe ohne Performance-Einbußen
- **Asynchrone Operationen** für reaktionsschnelle UI

### 💬 Chat-Funktionen
- **Vollständige Chat-Verwaltung** - Erstellen, Laden, Löschen von Konversationen
- **Persistente Speicherung** - Alle Chats werden lokal gespeichert
- **Konversationsverlauf** - Automatische Übertragung des Chat-Kontexts an Gemini
- **Intuitive Benutzeroberfläche** - Moderne, dunkle UI mit klarer Struktur

### 🔐 Sicherheit
- **Sichere API-Schlüssel-Speicherung** mit Windows Data Protection
- **Lokale Datenhaltung** - Keine Cloud-Abhängigkeit für Chat-Daten
- **Verschlüsselte Konfiguration** - API-Schlüssel werden verschlüsselt gespeichert

### 🎨 Benutzeroberfläche
- **Moderne dunkle UI** mit anpassbaren Farben
- **Responsive Design** - Anpassbare Fenstergröße und Seitenleiste
- **Einstellungsdialog** für API-Konfiguration
- **Status-Anzeigen** für bessere Benutzerführung

## Systemanforderungen

- **Windows 10/11** (x64)
- **.NET 8.0 Runtime** (wird automatisch installiert)
- **Internetverbindung** für Gemini API-Zugriff

## Installation

1. **Release herunterladen** von den [Releases](../../releases)
2. **Setup ausführen** - Die Anwendung wird automatisch installiert
3. **API-Schlüssel konfigurieren** über das Einstellungsmenü (⚙)

## Erste Schritte

### API-Schlüssel einrichten
1. Öffnen Sie [Google AI Studio](https://aistudio.google.com/)
2. Erstellen Sie einen neuen API-Schlüssel
3. Öffnen Sie Gemini GUI und klicken Sie auf das Einstellungssymbol (⚙)
4. Fügen Sie Ihren API-Schlüssel ein und testen Sie die Verbindung

### Ersten Chat starten
1. Klicken Sie auf "Neuer Chat" in der Seitenleiste
2. Geben Sie Ihre Nachricht in das Eingabefeld ein
3. Drücken Sie Enter oder klicken Sie auf "Senden"

## Technische Details

### Architektur
- **WPF (Windows Presentation Foundation)** für native Windows-UI
- **MVVM-Pattern** mit CommunityToolkit.Mvvm
- **Dependency Injection** mit Microsoft.Extensions.Hosting
- **SQLite** für lokale Datenbank
- **HTTP-Client** für Gemini API-Kommunikation

### Datenbank-Schema
```sql
-- Chats Tabelle
CREATE TABLE Chats (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT NOT NULL,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    MessageCount INTEGER DEFAULT 0,
    TotalTokens INTEGER DEFAULT 0
);

-- ChatMessages Tabelle
CREATE TABLE ChatMessages (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ChatId INTEGER NOT NULL,
    Role TEXT NOT NULL,
    Content TEXT NOT NULL,
    Timestamp TEXT NOT NULL,
    TokenCount INTEGER DEFAULT 0,
    FOREIGN KEY (ChatId) REFERENCES Chats (Id) ON DELETE CASCADE
);
```

### API-Integration
- **Gemini 2.0 Flash Experimental** Modell
- **Streaming-Unterstützung** für bessere Performance
- **Fehlerbehandlung** mit detaillierten Fehlermeldungen
- **Token-Tracking** für Kostenüberwachung

## Entwicklung

### Voraussetzungen
- **Visual Studio 2022** oder **Visual Studio Code**
- **.NET 8.0 SDK**
- **Windows SDK**

### Projekt erstellen
```bash
git clone <repository-url>
cd GeminiGUI
dotnet restore
dotnet build
dotnet run
```

### Abhängigkeiten
- `Microsoft.Data.Sqlite` - SQLite-Datenbank
- `CommunityToolkit.Mvvm` - MVVM-Framework
- `Microsoft.Extensions.Hosting` - Dependency Injection
- `System.Text.Json` - JSON-Serialisierung

## Lizenz

Dieses Projekt ist unter der MIT-Lizenz lizenziert. Siehe [LICENSE](LICENSE) für Details.

## Support

Bei Problemen oder Fragen:
1. Überprüfen Sie die [FAQ](../../wiki/FAQ)
2. Erstellen Sie ein [Issue](../../issues)
3. Kontaktieren Sie den Support

## Changelog

### Version 1.0.0
- ✅ Native Windows-Anwendung mit WPF
- ✅ Google Gemini 2.5 Pro Integration
- ✅ Lokale SQLite-Datenbank
- ✅ Sichere API-Schlüssel-Speicherung
- ✅ Chat-Management (Erstellen, Laden, Löschen)
- ✅ Moderne dunkle UI
- ✅ Performance-Optimierungen für große Chats

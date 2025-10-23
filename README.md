# Gemini GUI

A native Windows desktop application for Google Gemini Pro & Flash with local chat storage and modern user interface.

## Features

- **Chat Management**: Local storage of all conversations with SQLite database
- **Modern Interface**: Clean Windows 11 app design with light theme
- **Live Streaming**: Real-time responses from Gemini API
- **Secure Storage**: Encrypted API key storage using Windows Data Protection
- **Markdown Support**: Full markdown rendering including code blocks
- **User Experience**: Intuitive controls with keyboard shortcuts and context menus

## Installation

1. Download the latest release
2. Run `GeminiGUI.exe`
3. Enter your Google Gemini API key in Settings

## Requirements

- Windows 10 or later
- Google Gemini API key
- .NET 8.0 runtime (included in release)

## Usage

1. Create a new chat or select an existing one
2. Type your message in the input field
3. Press Enter or Ctrl+Enter to send
4. View responses with live streaming

## Technology Stack

- WPF (.NET 8)
- SQLite for local database
- Google Gemini Pro & Flash API
- CommunityToolkit.Mvvm for MVVM pattern
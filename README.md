# Search for Windows (working port)

A Windows-native reimplementation of [Search](https://github.com/driceroland/Search), the minimalist macOS browser by Office Commun. This repository is an independent Windows port; it does not contain or modify the upstream macOS source.

## Status

**Early foundation / not feature-parity.** The macOS app is a ~42k-line SwiftUI + AppKit + WKWebView browser. Windows cannot run that code directly. This port uses C#/.NET 8, WPF and Microsoft Edge WebView2 (Chromium). The first milestone is a usable minimal browser shell with tabs, address/search input, navigation and session basics. macOS-only services (iCloud sync, Handoff, Keychain, WKWebExtension) need Windows-native replacements and are not claimed to work.

## Requirements

- Windows 10 (1903+) or Windows 11, x64
- .NET 8 SDK
- Microsoft Edge WebView2 Runtime (normally installed with Edge)

## Build

```powershell
dotnet restore
dotnet build -c Release
dotnet run
```

## Current shortcuts

| Shortcut | Action |
|---|---|
| Ctrl+L | Focus address field |
| Ctrl+T | New tab |
| Ctrl+W | Close current tab |
| Ctrl+R / F5 | Reload |
| Alt+Left / Alt+Right | Back / forward |
| Enter in address field | Navigate or search |

## Architecture

- **WPF** owns the window chrome and tab strip.
- **WebView2** hosts web content using the installed Edge runtime.
- Browser state is deliberately kept separate from web content.
- Future milestones: persisted tabs/history/bookmarks, private profiles, content blocking, reader mode, per-site element hiding, downloads UI, picture-in-picture, extension support and settings.

## Upstream

- Original project: https://github.com/driceroland/Search
- Website: https://searchbrowser.app

Please consult the upstream repository's license and preserve its attribution when reusing any code or assets. This initial Windows foundation is a clean-room implementation of the browser shell and does not copy upstream source files.

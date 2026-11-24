# BBroswer — Windows browser project

BBroswer is an independent Windows implementation inspired by the minimalist browsing approach of [Search](https://github.com/driceroland/Search). It does not modify the upstream project and uses its own name and interface.

## Current status

This is an early, actively developed browser foundation—not a complete feature-for-feature port. It uses C#/.NET 8, WPF and Microsoft Edge WebView2 (Chromium).

## Included

- Refined dark interface with a compact tab sidebar and address toolbar
- Search/address navigation using DuckDuckGo
- Welcome page for new tabs
- Multiple tabs, tab switching, closing and session restoration
- Private tabs with separate temporary WebView2 data folders
- Local bookmarks, history and download records
- Basic request blocking with a small built-in host list
- Reader-style page appearance, picture-in-picture request and one-click element hiding
- Unpacked Chromium extension installation on regular tabs
- Keyboard shortcuts and download collision-safe filenames

## Still missing / known limitations

- Full EasyList/uBlock filter compatibility and per-site controls
- Complete extension manager, Chrome Web Store integration and extension parity
- Password manager, encrypted credential storage and password import
- Split view, pinned tabs, profiles/spaces and advanced session management
- Cloud sync, native updater and signed installer
- Full reader extraction, persistent element-hiding rules and complete download management
- Robust privacy guarantees: private tabs are isolated from the regular WebView2 profile, but Windows/WebView2 may retain operating-system traces

## Requirements

- Windows 10/11 x64
- Microsoft Edge WebView2 Runtime
- The self-contained CI artifact includes the .NET app runtime; WebView2 Runtime remains a system dependency

Browser data and the existing WebView2 profile are kept in `%LOCALAPPDATA%\\SearchWindows` so upgrades from the earlier build retain saved data and browser sessions.\n\n## Run from source

Install the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0), then:

```powershell
dotnet restore Search.Windows.csproj
dotnet run --project Search.Windows.csproj
```

## Download a Windows build

Open [GitHub Actions](https://github.com/DOckTV/BBroswer/actions), choose the latest successful **Windows build**, and download the `BBroswer-Windows-x64` artifact. Extract the ZIP and run `BBroswer.exe`.

## Shortcuts

| Shortcut | Action |
|---|---|
| Ctrl+L | Focus address field |
| Ctrl+T | New tab |
| Ctrl+W | Close tab |
| Ctrl+R | Reload |
| Ctrl+D | Bookmark current page |
| Ctrl+H | History |
| Ctrl+J | Downloads |
| Ctrl+Shift+N | New private tab |
| Enter in address field | Navigate or search |

## Upstream

- Original project: https://github.com/driceroland/Search
- Website: https://searchbrowser.app

Please retain upstream attribution when reusing upstream code or assets. The Windows-specific implementation is independently written.

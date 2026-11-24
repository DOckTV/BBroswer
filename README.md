# BBroswer — Windows port of Search

An independent Windows-native reimplementation inspired by [Search](https://github.com/driceroland/Search), the minimalist macOS browser by Office Commun. This repository is a clean-room Windows implementation and does not modify the upstream project.

## Build status

This is an **early, actively developed port**, not full feature parity yet. It uses C#/.NET 8, WPF and Microsoft Edge WebView2 (Chromium). The Windows CI workflow builds the app and publishes a downloadable x64 artifact.

## Implemented in the current code

- Minimal dark UI with a left-side tab rail
- Multiple tabs, tab switching and closing
- Address/search field with URL detection
- Back, forward, reload and keyboard shortcuts
- Session restoration (up to 12 restored tabs; 30 saved)
- Local JSON storage for bookmarks, history and downloads
- Private tabs using isolated temporary WebView2 data folders; private visits are excluded from saved history/session
- Basic network request blocking using a small built-in domain denylist
- Reader-style page formatting toggle
- Unpacked Chromium extension installation through WebView2's extension API (regular profile only)
- Picture-in-picture request for the page's first video
- Hide-one-element mode (next click)
- Download routing to the user's Downloads folder

## Not yet implemented / limitations

- Full EasyList/uBlock filter compatibility and per-site block controls
- Full Chrome Web Store integration and extension management UI
- Password manager, encrypted credential storage and import
- Split view, pinned tabs, profiles/spaces and advanced session model
- Cloud sync / Handoff (Apple-only features need a separate Windows sync design)
- Native app updater and signed installer
- Full reader extraction, persistent element-hiding rules and download manager controls

The current private-tab isolation uses a separate temporary WebView2 user-data folder per private tab. Closing a private tab attempts to remove that folder. It is not a forensic privacy guarantee; WebView2 and Windows may retain OS-level traces.

## Requirements

- Windows 10 or Windows 11, x64
- .NET 8 runtime for framework-dependent local builds, or use the self-contained CI artifact
- Microsoft Edge WebView2 Runtime (normally present on Windows with Edge)

## Build locally

```powershell
dotnet restore Search.Windows.csproj
dotnet build Search.Windows.csproj -c Release
dotnet run --project Search.Windows.csproj
```

## Download CI build

Open [Actions](https://github.com/DOckTV/BBroswer/actions), select the latest successful **Windows build**, and download the `BBroswer-Windows-x64` artifact. Extract it and run `Search.Windows.exe`. WebView2 Runtime is still required.

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
| Enter in address field | Navigate or search |

## Upstream

- Original source: https://github.com/driceroland/Search
- Website: https://searchbrowser.app

Please retain upstream attribution when reusing upstream code or assets. The current browser UI and Windows-specific code are independently written.

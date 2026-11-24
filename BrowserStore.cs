using System.Text.Json;

namespace Search.Windows;

public sealed class BrowserStore
{
    private readonly string _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SearchWindows");
    private string FilePath => Path.Combine(_folder, "browser-data.json");
    public BrowserState State { get; private set; } = new();

    public async Task LoadAsync()
    {
        try
        {
            Directory.CreateDirectory(_folder);
            if (File.Exists(FilePath))
                State = JsonSerializer.Deserialize<BrowserState>(await File.ReadAllTextAsync(FilePath)) ?? new BrowserState();
        }
        catch { State = new BrowserState(); }
    }

    public async Task SaveAsync()
    {
        try
        {
            Directory.CreateDirectory(_folder);
            var temp = FilePath + ".tmp";
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(State, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, FilePath, true);
        }
        catch { }
    }

    public void AddHistory(string title, string url)
    {
        State.History.Add(new HistoryEntry(title, url, DateTimeOffset.UtcNow));
        if (State.History.Count > 5000) State.History.RemoveRange(0, State.History.Count - 5000);
    }

    public void AddBookmark(string title, string url)
    {
        if (!State.Bookmarks.Any(x => x.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
            State.Bookmarks.Add(new BookmarkEntry(title, url));
    }

    public void AddDownload(string name, string path) => State.Downloads.Add(new DownloadEntry(name, path, DateTimeOffset.UtcNow));
}

public sealed class BrowserState
{
    public List<string> OpenTabs { get; set; } = new();
    public string? LastUrl { get; set; }
    public List<BookmarkEntry> Bookmarks { get; set; } = new();
    public List<HistoryEntry> History { get; set; } = new();
    public List<DownloadEntry> Downloads { get; set; } = new();
}
public sealed record BookmarkEntry(string Title, string Url);
public sealed record HistoryEntry(string Title, string Url, DateTimeOffset VisitedAt);
public sealed record DownloadEntry(string Name, string Path, DateTimeOffset StartedAt);

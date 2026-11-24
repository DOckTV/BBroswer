using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Search.Windows;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<BrowserTab> _tabs = new();
    private readonly BrowserStore _store = new();
    private BrowserTab? _active;
    private bool _switching;
    private bool _adBlockEnabled = true;

    public MainWindow()
    {
        InitializeComponent();
        TabsList.ItemsSource = _tabs;
        Loaded += async (_, _) =>
        {
            await _store.LoadAsync();
            foreach (var url in _store.State.OpenTabs.Take(12)) await CreateTabAsync(url, false);
            if (_tabs.Count == 0) await CreateTabAsync("about:blank", false);
            if (_store.State.LastUrl is { Length: > 0 } last && _tabs.Count == 1 && _tabs[0].Url == "about:blank")
                Navigate(_tabs[0], last);
        };
        Closing += async (_, _) => await SaveSessionAsync();
    }

    private async Task CreateTabAsync(string? address = null, bool select = true, bool isPrivate = false)
    {
        var tab = new BrowserTab { IsPrivate = isPrivate, DisplayTitle = isPrivate ? "Private tab" : "New tab" };
        var view = new WebView2();
        tab.View = view;
        _tabs.Add(tab);
        BrowserHost.Children.Add(view);
        view.Visibility = Visibility.Collapsed;
        if (select) SelectTab(tab);

        tab.UserDataPath = isPrivate
            ? Path.Combine(Path.GetTempPath(), "SearchWindowsPrivate", Guid.NewGuid().ToString("N"))
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SearchWindows", "WebView2");
        Directory.CreateDirectory(tab.UserDataPath);
        var options = new CoreWebView2EnvironmentOptions { AreBrowserExtensionsEnabled = !isPrivate };
        var env = await CoreWebView2Environment.CreateAsync(null, tab.UserDataPath, options);
        await view.EnsureCoreWebView2Async(env);
        var core = view.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = true;
        core.Settings.AreDevToolsEnabled = true;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);

        core.NewWindowRequested += (_, e) => { e.Handled = true; _ = CreateTabAsync(e.Uri, true, isPrivate); };
        core.DocumentTitleChanged += (_, _) =>
        {
            tab.DisplayTitle = string.IsNullOrWhiteSpace(core.DocumentTitle) ? "New tab" : core.DocumentTitle;
            TabsList.Items.Refresh();
        };
        core.SourceChanged += (_, _) =>
        {
            tab.Url = core.Source?.ToString() ?? "";
            if (_active == tab) AddressBox.Text = tab.Url;
        };
        core.WebResourceRequested += (_, e) =>
        {
            if (_adBlockEnabled && AdBlocker.ShouldBlock(e.Request.Uri))
                e.Response = core.Environment.CreateWebResourceResponse(null, 204, "Blocked", "");
        };
        core.NavigationStarting += (_, _) => { if (_active == tab) StatusText.Text = "Loading…"; };
        core.NavigationCompleted += async (_, e) =>
        {
            if (e.IsSuccess)
            {
                tab.Url = core.Source?.ToString() ?? "";
                if (!tab.IsPrivate && Uri.TryCreate(tab.Url, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
                {
                    _store.AddHistory(tab.DisplayTitle, tab.Url);
                    await _store.SaveAsync();
                }
                if (_active == tab) StatusText.Text = tab.IsPrivate ? "Private browsing · this tab is isolated" : "Ready";
            }
            else if (_active == tab) StatusText.Text = $"Page failed: {e.WebErrorStatus}";
        };
        core.DownloadStarting += (_, e) =>
        {
            var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Directory.CreateDirectory(downloads);
            var name = Path.GetFileName(e.ResultFilePath);
            if (string.IsNullOrWhiteSpace(name)) name = "download";
            e.ResultFilePath = Path.Combine(downloads, name);
            _store.AddDownload(name, e.ResultFilePath);
            _ = _store.SaveAsync();
        };
        Navigate(tab, address ?? "about:blank");
        if (select) SelectTab(tab);
    }

    private void SelectTab(BrowserTab tab)
    {
        _switching = true;
        if (_active?.View is not null) _active.View.Visibility = Visibility.Collapsed;
        _active = tab;
        if (tab.View is not null) tab.View.Visibility = Visibility.Visible;
        TabsList.SelectedItem = tab;
        AddressBox.Text = tab.Url;
        _switching = false;
        StatusText.Text = tab.IsPrivate ? "Private browsing · history is not saved" : "Ready";
    }

    private void TabsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_switching && TabsList.SelectedItem is BrowserTab tab) SelectTab(tab);
    }

    private static void Navigate(BrowserTab tab, string input)
    {
        var core = tab.View?.CoreWebView2;
        if (core is null) { tab.PendingAddress = input; return; }
        var value = input.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) { core.Navigate(value); return; }
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == "http" || uri.Scheme == "https"))
            core.Navigate(uri.ToString());
        else if (value.Contains(' ') || !value.Contains('.'))
            core.Navigate("https://www.google.com/search?q=" + Uri.EscapeDataString(value));
        else core.Navigate("https://" + value);
    }

    private async Task SaveSessionAsync()
    {
        _store.State.OpenTabs = _tabs.Where(t => !t.IsPrivate).Select(t => t.Url).Where(u => !string.IsNullOrWhiteSpace(u)).Take(30).ToList();
        _store.State.LastUrl = _active is { IsPrivate: false } ? _active.Url : null;
        await _store.SaveAsync();
    }

    private async void NewTab_Click(object sender, RoutedEventArgs e) => await CreateTabAsync("about:blank");
    private async void NewPrivateTab_Click(object sender, RoutedEventArgs e) => await CreateTabAsync("about:blank", true, true);

    private async void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not BrowserTab tab) return;
        var index = _tabs.IndexOf(tab);
        BrowserHost.Children.Remove(tab.View);
        tab.View?.Dispose();
        _tabs.Remove(tab);
        if (tab.IsPrivate && tab.UserDataPath is not null)
        {
            try { Directory.Delete(tab.UserDataPath, true); } catch { }
        }
        if (_tabs.Count == 0) await CreateTabAsync("about:blank");
        else SelectTab(_tabs[Math.Clamp(index - 1, 0, _tabs.Count - 1)]);
        await SaveSessionAsync();
    }

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _active is null) return;
        Navigate(_active, AddressBox.Text);
        Keyboard.ClearFocus();
        e.Handled = true;
    }
    private void AddressBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => AddressBox.SelectAll();
    private void Back_Click(object sender, RoutedEventArgs e) { if (_active?.View?.CoreWebView2?.CanGoBack == true) _active.View.GoBack(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { if (_active?.View?.CoreWebView2?.CanGoForward == true) _active.View.GoForward(); }
    private void Reload_Click(object sender, RoutedEventArgs e) => _active?.View?.Reload();

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if (_active is null || _active.IsPrivate || string.IsNullOrWhiteSpace(_active.Url)) return;
        _store.AddBookmark(_active.DisplayTitle, _active.Url);
        _ = _store.SaveAsync();
        StatusText.Text = "Bookmark saved";
    }

    private void ShowBookmarks_Click(object sender, RoutedEventArgs e) => ShowList("Bookmarks", _store.State.Bookmarks.Select(x => $"{x.Title}\n{x.Url}"));
    private void ShowHistory_Click(object sender, RoutedEventArgs e) => ShowList("History", _store.State.History.TakeLast(200).Reverse().Select(x => $"{x.Title}\n{x.Url}"));
    private void ShowDownloads_Click(object sender, RoutedEventArgs e) => ShowList("Downloads", _store.State.Downloads.TakeLast(100).Reverse().Select(x => $"{x.Name}\n{x.Path}"));

    private void ShowList(string title, IEnumerable<string> values)
    {
        var list = new ListBox { ItemsSource = values, Background = System.Windows.Media.Brushes.Transparent, Foreground = System.Windows.Media.Brushes.White, Padding = new Thickness(12) };
        var win = new Window { Title = title, Owner = this, Width = 620, Height = 520, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30,30,30)), Content = list };
        win.ShowDialog();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Margin = new Thickness(20) };
        var block = new CheckBox { Content = "Block known advertising and tracking hosts", IsChecked = _adBlockEnabled, Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 10, 0, 20) };
        var note = new TextBlock { Text = "Install unpacked Chromium extensions from a local folder containing manifest.json. Private tabs do not load extensions. Cloud sync and password migration are still future work.", Foreground = System.Windows.Media.Brushes.LightGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20) };
        var win = new Window { Title = "Settings", Owner = this, Width = 480, Height = 280, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34,34,34)), Content = panel };
        var install = new Button { Content = "Install unpacked extension", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 12) };
        install.Click += async (_, _) => await InstallExtensionAsync();
        var save = new Button { Content = "Save settings", HorizontalAlignment = HorizontalAlignment.Right };
        save.Click += (_, _) => { _adBlockEnabled = block.IsChecked == true; win.Close(); };
        panel.Children.Add(block); panel.Children.Add(note); panel.Children.Add(install); panel.Children.Add(save);
        win.ShowDialog();
    }


    private async Task InstallExtensionAsync()
    {
        if (_active?.View?.CoreWebView2 is not { } core)
        {
            MessageBox.Show(this, "Open a regular browser tab before installing an extension.", "Extensions");
            return;
        }
        var picker = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select an unpacked Chromium extension folder",
            Multiselect = false
        };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var extension = await core.Profile.AddBrowserExtensionAsync(picker.FolderName);
            MessageBox.Show(this, $"Installed {extension.Name}. It is enabled for this browser profile.", "Extension installed");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not install extension. Select the unpacked extension's top-level folder containing manifest.json.\\n\\n{ex.Message}", "Extension install failed");
        }
    }

    private void PageMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_active?.View?.CoreWebView2 is not { } core) return;
        var menu = new ContextMenu();
        AddMenu(menu, "Reader mode", async () => await core.ExecuteScriptAsync("(()=>{let old=document.getElementById('__search_reader');if(old){old.remove();return;}let s=document.createElement('style');s.id='__search_reader';s.textContent='body{max-width:760px!important;margin:40px auto!important;padding:40px!important;font:20px/1.75 Georgia,serif!important;background:#f5f1e8!important;color:#222!important}img,video{max-width:100%!important;height:auto!important}';document.head.appendChild(s);})();"));
        AddMenu(menu, "Picture in picture", async () => await core.ExecuteScriptAsync("document.querySelector('video')?.requestPictureInPicture?.()"));
        AddMenu(menu, "Hide clicked element", async () => await core.ExecuteScriptAsync("(()=>{if(window.__searchHideMode)return;window.__searchHideMode=true;document.addEventListener('click',function h(e){e.preventDefault();e.stopPropagation();e.target.style.setProperty('display','none','important');document.removeEventListener('click',h,true);window.__searchHideMode=false;},true);})();"));
        AddMenu(menu, "Find on page", async () => await core.ExecuteScriptAsync("window.find(prompt('Find text on this page')||'')"));
        menu.IsOpen = true;
    }

    private static void AddMenu(ContextMenu menu, string label, Func<Task> action)
    {
        var item = new MenuItem { Header = label };
        item.Click += async (_, _) => await action();
        menu.Items.Add(item);
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        switch (e.Key)
        {
            case Key.L: AddressBox.Focus(); AddressBox.SelectAll(); e.Handled = true; break;
            case Key.T: await CreateTabAsync("about:blank"); e.Handled = true; break;
            case Key.W: if (_active is not null) CloseTab_Click(new Button { Tag = _active }, new RoutedEventArgs()); e.Handled = true; break;
            case Key.R: _active?.View?.Reload(); e.Handled = true; break;
            case Key.D: Bookmark_Click(this, new RoutedEventArgs()); e.Handled = true; break;
            case Key.H: ShowHistory_Click(this, new RoutedEventArgs()); e.Handled = true; break;
            case Key.J: ShowDownloads_Click(this, new RoutedEventArgs()); e.Handled = true; break;
        }
    }

    private sealed class BrowserTab
    {
        public string DisplayTitle { get; set; } = "New tab";
        public string Url { get; set; } = "";
        public string? PendingAddress { get; set; }
        public bool IsPrivate { get; set; }
        public string? UserDataPath { get; set; }
        public WebView2? View { get; set; }
    }
}

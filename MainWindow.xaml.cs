using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Search.Windows;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<BrowserTab> _tabs = new();
    private readonly BrowserStore _store = new();
    private BrowserTab? _active;
    private CoreWebView2Environment? _regularEnvironment;
    private bool _switchingTabs;
    private bool _allowClose;
    private bool _adBlockEnabled = true;

    private const string HomePage = """
        <!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width">
        <style>
        *{box-sizing:border-box}body{margin:0;background:#f7f8f6;color:#202923;font:15px/1.5 'Segoe UI',sans-serif;min-height:100vh;display:grid;place-items:center}
        main{width:min(620px,88vw);margin-top:-7vh}small{letter-spacing:.18em;color:#718078;font-size:11px;font-weight:700}
        h1{font-size:46px;letter-spacing:-.055em;font-weight:500;margin:12px 0 8px}p{color:#718078;margin:0 0 28px}
        form{display:flex;background:white;border:1px solid #dfe5df;border-radius:14px;padding:7px;box-shadow:0 8px 35px #1c302010}
        input{flex:1;min-width:0;border:0;outline:0;padding:12px 14px;font:inherit;color:#202923}button{border:0;border-radius:10px;background:#b7d8c5;color:#18231d;padding:0 19px;font:600 13px 'Segoe UI';cursor:pointer}
        .hint{margin-top:18px;font-size:12px;color:#89948d}a{color:#526e5d;text-decoration:none}
        </style></head><body><main><small>BBROWSER · WINDOWS</small><h1>Make room to think.</h1><p>A quieter place to browse.</p>
        <form action="https://duckduckgo.com/" method="get"><input name="q" autofocus placeholder="Search the web or enter an address" aria-label="Search"><button>Search</button></form>
        <div class="hint">A small, independent browser project. <a href="https://searchbrowser.app">About the inspiration</a></div></main></body></html>
        """;

    public MainWindow()
    {
        InitializeComponent();
        TabsList.ItemsSource = _tabs;
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _store.LoadAsync();
            _adBlockEnabled = _store.State.AdBlockEnabled;
            var restore = _store.State.OpenTabs.Take(12).ToList();
            if (restore.Count == 0) await CreateTabAsync();
            else
            {
                foreach (var url in restore) await CreateTabAsync(url, false);
                SelectTab(_tabs.First());
                if (!string.IsNullOrWhiteSpace(_store.State.LastUrl))
                {
                    var last = _tabs.FirstOrDefault(t => t.Url == _store.State.LastUrl);
                    if (last is not null) SelectTab(last);
                }
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = "Startup issue";
            MessageBox.Show(this, "BBroswer couldn't finish starting.\n\n" + ex.Message, "Startup error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task<CoreWebView2Environment> GetEnvironmentAsync(bool isPrivate, string userDataPath)
    {
        if (isPrivate)
            return await CoreWebView2Environment.CreateAsync(null, userDataPath,
                new CoreWebView2EnvironmentOptions { AreBrowserExtensionsEnabled = false });
        if (_regularEnvironment is null)
            _regularEnvironment = await CoreWebView2Environment.CreateAsync(null, userDataPath,
                new CoreWebView2EnvironmentOptions { AreBrowserExtensionsEnabled = true });
        return _regularEnvironment;
    }

    private async Task CreateTabAsync(string? address = null, bool select = true, bool isPrivate = false)
    {
        var tab = new BrowserTab { IsPrivate = isPrivate, DisplayTitle = isPrivate ? "Private tab" : "New tab" };
        var view = new WebView2();
        tab.View = view;
        _tabs.Add(tab);
        BrowserHost.Children.Add(view);
        view.Visibility = Visibility.Collapsed;
        tab.UserDataPath = isPrivate
            ? Path.Combine(Path.GetTempPath(), "BBroswerPrivate", Guid.NewGuid().ToString("N"))
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BBroswer", "WebView2");
        Directory.CreateDirectory(tab.UserDataPath);

        try
        {
            var environment = await GetEnvironmentAsync(isPrivate, tab.UserDataPath);
            await view.EnsureCoreWebView2Async(environment);
            var core = view.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = true;
            core.Settings.AreDevToolsEnabled = true;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);

            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                _ = CreateTabAsync(args.Uri, true, isPrivate);
            };
            core.DocumentTitleChanged += (_, _) =>
                tab.DisplayTitle = string.IsNullOrWhiteSpace(core.DocumentTitle) || core.DocumentTitle == "about:blank"
                    ? (tab.IsPrivate ? "Private tab" : "New tab") : core.DocumentTitle;
            core.SourceChanged += (_, _) =>
            {
                var source = core.Source?.ToString() ?? "";
                tab.Url = source == "about:blank" ? "" : source;
                if (_active == tab) AddressBox.Text = tab.Url;
                UpdateNavigationButtons();
            };
            core.NavigationStarting += (_, args) =>
            {
                if (_active == tab) StatusText.Text = "Loading page…";
                if (_active == tab && !string.IsNullOrWhiteSpace(args.Uri) && args.Uri != "about:blank")
                    AddressBox.Text = args.Uri;
            };
            core.NavigationCompleted += async (_, args) =>
            {
                if (args.IsSuccess)
                {
                    tab.Url = core.Source?.ToString() == "about:blank" ? "" : core.Source?.ToString() ?? "";
                    if (!tab.IsPrivate && Uri.TryCreate(tab.Url, UriKind.Absolute, out var uri) &&
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                    {
                        _store.AddHistory(tab.DisplayTitle, tab.Url);
                        await _store.SaveAsync();
                    }
                    if (_active == tab) StatusText.Text = tab.IsPrivate ? "Private tab · local history is not saved" : "Ready";
                }
                else if (_active == tab) StatusText.Text = $"Couldn't load page · {args.WebErrorStatus}";
                UpdateNavigationButtons();
            };
            core.WebResourceRequested += (_, args) =>
            {
                if (_adBlockEnabled && AdBlocker.ShouldBlock(args.Request.Uri))
                    args.Response = core.Environment.CreateWebResourceResponse(null, 204, "Blocked", "");
            };
            core.DownloadStarting += (_, args) =>
            {
                var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                Directory.CreateDirectory(downloads);
                var name = Path.GetFileName(args.ResultFilePath);
                if (string.IsNullOrWhiteSpace(name)) name = "download";
                var destination = Path.Combine(downloads, name);
                if (File.Exists(destination))
                {
                    var stem = Path.GetFileNameWithoutExtension(name);
                    var ext = Path.GetExtension(name);
                    var i = 2;
                    while (File.Exists(destination))
                    {
                        destination = Path.Combine(downloads, $"{stem} ({i++}){ext}");
                    }
                }
                args.ResultFilePath = destination;
                _store.AddDownload(Path.GetFileName(destination), destination);
                _ = _store.SaveAsync();
            };

            if (!string.IsNullOrWhiteSpace(address)) Navigate(tab, address);
            else ShowHome(tab);
            if (select) SelectTab(tab);
        }
        catch (Exception ex)
        {
            _tabs.Remove(tab);
            BrowserHost.Children.Remove(view);
            view.Dispose();
            if (isPrivate && tab.UserDataPath is not null) TryDeleteDirectory(tab.UserDataPath);
            MessageBox.Show(this, "This tab couldn't be initialized. Check that Microsoft Edge WebView2 Runtime is installed.\n\n" + ex.Message,
                "Tab initialization failed", MessageBoxButton.OK, MessageBoxImage.Error);
            if (_tabs.Count == 0) StatusText.Text = "WebView2 needs attention";
        }
    }

    private void ShowHome(BrowserTab tab)
    {
        if (tab.View?.CoreWebView2 is null) return;
        tab.Url = "";
        tab.DisplayTitle = tab.IsPrivate ? "Private tab" : "New tab";
        tab.View.CoreWebView2.NavigateToString(HomePage);
        if (_active == tab) AddressBox.Text = "";
    }

    private void SelectTab(BrowserTab tab)
    {
        if (!_tabs.Contains(tab)) return;
        _switchingTabs = true;
        if (_active?.View is not null) _active.View.Visibility = Visibility.Collapsed;
        _active = tab;
        if (tab.View is not null) tab.View.Visibility = Visibility.Visible;
        TabsList.SelectedItem = tab;
        AddressBox.Text = tab.Url;
        StatusText.Text = tab.IsPrivate ? "Private tab · local history is not saved" : "Ready";
        _switchingTabs = false;
        UpdateNavigationButtons();
    }

    private void UpdateNavigationButtons()
    {
        BackButton.IsEnabled = _active?.View?.CoreWebView2?.CanGoBack == true;
        ForwardButton.IsEnabled = _active?.View?.CoreWebView2?.CanGoForward == true;
    }

    private void TabsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_switchingTabs && TabsList.SelectedItem is BrowserTab tab) SelectTab(tab);
    }

    private void Navigate(BrowserTab tab, string input)
    {
        var core = tab.View?.CoreWebView2;
        if (core is null) return;
        var value = input.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) { core.Navigate(value); return; }
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            core.Navigate(uri.ToString());
        else if (value.Contains(' ') || !value.Contains('.'))
            core.Navigate("https://duckduckgo.com/?q=" + Uri.EscapeDataString(value));
        else
            core.Navigate("https://" + value);
    }

    private async Task SaveSessionAsync()
    {
        _store.State.OpenTabs = _tabs.Where(t => !t.IsPrivate)
            .Select(t => t.Url).Where(u => !string.IsNullOrWhiteSpace(u)).Take(30).ToList();
        _store.State.LastUrl = _active is { IsPrivate: false } ? _active.Url : null;
        _store.State.AdBlockEnabled = _adBlockEnabled;
        await _store.SaveAsync();
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        await SaveSessionAsync();
        _allowClose = true;
        Close();
    }

    private async void NewTab_Click(object sender, RoutedEventArgs e) => await CreateTabAsync();
    private async void NewPrivateTab_Click(object sender, RoutedEventArgs e) => await CreateTabAsync(null, true, true);

    private async void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not BrowserTab tab) return;
        var index = _tabs.IndexOf(tab);
        var wasActive = _active == tab;
        _tabs.Remove(tab);
        if (wasActive) _active = null;
        if (tab.View is not null)
        {
            BrowserHost.Children.Remove(tab.View);
            tab.View.Dispose();
        }
        if (tab.IsPrivate && tab.UserDataPath is not null)
        {
            await Task.Delay(250);
            TryDeleteDirectory(tab.UserDataPath);
        }
        if (_tabs.Count == 0) await CreateTabAsync();
        else if (wasActive) SelectTab(_tabs[Math.Clamp(index - 1, 0, _tabs.Count - 1)]);
        await SaveSessionAsync();
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
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
    private void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_active?.View?.CoreWebView2 is { } core && !string.IsNullOrWhiteSpace(_active.Url)) core.Reload();
        else if (_active is not null) ShowHome(_active);
    }

    private void Bookmark_Click(object sender, RoutedEventArgs e)
    {
        if (_active is null || _active.IsPrivate || !Uri.TryCreate(_active.Url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            StatusText.Text = "Open a regular web page to bookmark it";
            return;
        }
        _store.AddBookmark(_active.DisplayTitle, _active.Url);
        _ = _store.SaveAsync();
        StatusText.Text = "Bookmark saved";
    }

    private void ShowBookmarks_Click(object sender, RoutedEventArgs e) =>
        ShowList("Bookmarks", _store.State.Bookmarks.Select(x => (x.Title, x.Url)));
    private void ShowHistory_Click(object sender, RoutedEventArgs e) =>
        ShowList("History", _store.State.History.TakeLast(300).Reverse().Select(x => (x.Title, x.Url)));
    private void ShowDownloads_Click(object sender, RoutedEventArgs e) =>
        ShowList("Downloads", _store.State.Downloads.TakeLast(150).Reverse().Select(x => (x.Name, x.Path)), true);

    private void ShowList(string title, IEnumerable<(string Title, string Url)> values, bool isDownload = false)
    {
        var entries = values.ToList();
        var list = new ListBox { Margin = new Thickness(12), Background = Brushes.Transparent };
        list.ItemTemplate = new DataTemplate();
        list.ItemsSource = entries.Select(x => new ListEntry(x.Title, x.Url)).ToList();
        var template = new DataTemplate(typeof(ListEntry));
        var root = new FrameworkElementFactory(typeof(StackPanel));
        root.SetValue(StackPanel.MarginProperty, new Thickness(8, 7, 8, 7));
        var heading = new FrameworkElementFactory(typeof(TextBlock));
        heading.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(ListEntry.Title)));
        heading.SetValue(TextBlock.ForegroundProperty, Brushes.White);
        heading.SetValue(TextBlock.FontSizeProperty, 13d);
        var sub = new FrameworkElementFactory(typeof(TextBlock));
        sub.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(ListEntry.Url)));
        sub.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(143, 154, 161)));
        sub.SetValue(TextBlock.FontSizeProperty, 11d);
        sub.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        root.AppendChild(heading); root.AppendChild(sub); template.VisualTree = root; list.ItemTemplate = template;
        var window = new Window
        {
            Title = title, Owner = this, Width = 650, Height = 540, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(25, 29, 32)), Foreground = Brushes.White,
            Content = list
        };
        list.MouseDoubleClick += (_, _) =>
        {
            if (list.SelectedItem is not ListEntry entry) return;
            if (isDownload)
            {
                try { Process.Start(new ProcessStartInfo(entry.Url) { UseShellExecute = true }); }
                catch { StatusText.Text = "Couldn't open that download"; }
            }
            else if (_active is not null) Navigate(_active, entry.Url);
            window.Close();
        };
        window.ShowDialog();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        var heading = new TextBlock { Text = "Browsing", FontSize = 21, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 18) };
        var block = new CheckBox { Content = "Block known advertising and tracking hosts", IsChecked = _adBlockEnabled, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 18) };
        var note = new TextBlock
        {
            Text = "This is a lightweight starter blocklist, not full EasyList or uBlock Origin compatibility. Extensions can be installed from an unpacked Chromium extension folder on a regular tab.",
            Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20)
        };
        var install = new Button { Content = "Install unpacked extension", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 20) };
        install.Click += async (_, _) => await InstallExtensionAsync();
        var save = new Button { Content = "Save settings", Background = new SolidColorBrush(Color.FromRgb(183, 216, 197)), Foreground = new SolidColorBrush(Color.FromRgb(24, 35, 29)), FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right };
        var window = new Window { Title = "Settings", Owner = this, Width = 500, Height = 330, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new SolidColorBrush(Color.FromRgb(29, 33, 37)), Content = panel };
        save.Click += async (_, _) => { _adBlockEnabled = block.IsChecked == true; await SaveSessionAsync(); window.Close(); };
        panel.Children.Add(heading); panel.Children.Add(block); panel.Children.Add(note); panel.Children.Add(install); panel.Children.Add(save);
        window.ShowDialog();
    }

    private async Task InstallExtensionAsync()
    {
        if (_active?.IsPrivate != false || _active.View?.CoreWebView2 is not { } core)
        {
            MessageBox.Show(this, "Open a regular browser tab before installing an extension.", "Extensions", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var picker = new Microsoft.Win32.OpenFolderDialog { Title = "Select an unpacked Chromium extension folder", Multiselect = false };
        if (picker.ShowDialog(this) != true) return;
        try
        {
            var extension = await core.Profile.AddBrowserExtensionAsync(picker.FolderName);
            MessageBox.Show(this, $"Installed {extension.Name}. Restart the browser if it doesn't appear immediately.", "Extension installed", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Choose the extension's top-level folder containing manifest.json.\n\n" + ex.Message, "Extension install failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PageMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_active?.View?.CoreWebView2 is not { } core) return;
        var menu = new ContextMenu();
        AddMenu(menu, "Reader appearance", async () => await core.ExecuteScriptAsync("(()=>{let old=document.getElementById('__bb_reader');if(old){old.remove();return;}let s=document.createElement('style');s.id='__bb_reader';s.textContent='body{max-width:760px!important;margin:40px auto!important;padding:40px!important;font:20px/1.75 Georgia,serif!important;background:#f5f1e8!important;color:#222!important}img,video{max-width:100%!important;height:auto!important}';document.head.appendChild(s);})();"));
        AddMenu(menu, "Picture in picture", async () => await core.ExecuteScriptAsync("document.querySelector('video')?.requestPictureInPicture?.()"));
        AddMenu(menu, "Hide next clicked element", async () => await core.ExecuteScriptAsync("(()=>{if(window.__bbHideMode)return;window.__bbHideMode=true;document.addEventListener('click',function h(e){e.preventDefault();e.stopPropagation();e.target.style.setProperty('display','none','important');document.removeEventListener('click',h,true);window.__bbHideMode=false;},true);})();"));
        AddMenu(menu, "Find on page", async () => await core.ExecuteScriptAsync("window.find(prompt('Find text on this page')||'')"));
        menu.IsOpen = true;
    }

    private static void AddMenu(ContextMenu menu, string label, Func<Task> action)
    {
        var item = new MenuItem { Header = label };
        item.Click += async (_, _) => { try { await action(); } catch { } };
        menu.Items.Add(item);
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.L: AddressBox.Focus(); AddressBox.SelectAll(); e.Handled = true; break;
                case Key.T: await CreateTabAsync(); e.Handled = true; break;
                case Key.W: if (_active is not null) CloseTab_Click(new Button { Tag = _active }, new RoutedEventArgs()); e.Handled = true; break;
                case Key.R: Reload_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.D: Bookmark_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.H: ShowHistory_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.J: ShowDownloads_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.F: PageMenu_Click(this, new RoutedEventArgs()); e.Handled = true; break;
            }
        }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.N)
        {
            await CreateTabAsync(null, true, true);
            e.Handled = true;
        }
    }

    private sealed class ListEntry(string title, string url)
    {
        public string Title { get; } = title;
        public string Url { get; } = url;
    }

    private sealed class BrowserTab : INotifyPropertyChanged
    {
        private string _displayTitle = "New tab";
        private string _url = "";
        public string DisplayTitle { get => _displayTitle; set { _displayTitle = value; OnPropertyChanged(); } }
        public string Url { get => _url; set { _url = value; OnPropertyChanged(); } }
        public bool IsPrivate { get; init; }
        public string? UserDataPath { get; set; }
        public WebView2? View { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
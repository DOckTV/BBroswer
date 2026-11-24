using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Search.Windows;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<BrowserTab> _tabs = new();
    private BrowserTab? _activeTab;

    public MainWindow()
    {
        InitializeComponent();
        TabItems.ItemsSource = _tabs;
        Loaded += async (_, _) => await CreateTabAsync();
    }

    private async Task CreateTabAsync(string? address = null)
    {
        var tab = new BrowserTab();
        var view = new WebView2();
        tab.View = view;
        tab.Title = "New tab";
        _tabs.Add(tab);
        SelectTab(tab);

        BrowserHost.Children.Add(view);
        await view.EnsureCoreWebView2Async();

        view.CoreWebView2.DocumentTitleChanged += (_, _) =>
        {
            tab.Title = string.IsNullOrWhiteSpace(view.CoreWebView2.DocumentTitle)
                ? "New tab" : view.CoreWebView2.DocumentTitle;
            RefreshTabs();
        };
        view.CoreWebView2.SourceChanged += (_, _) =>
        {
            if (_activeTab == tab) AddressBox.Text = view.Source?.ToString() ?? "";
        };
        view.CoreWebView2.NavigationStarting += (_, e) =>
        {
            if (_activeTab == tab) StatusText.Text = $"Loading {e.Uri}";
        };
        view.CoreWebView2.NavigationCompleted += (_, e) =>
        {
            if (_activeTab == tab)
                StatusText.Text = e.IsSuccess ? " " : $"Could not load page: {e.WebErrorStatus}";
        };

        Navigate(view, address ?? "https://www.google.com");
        AddressBox.Text = address ?? "";
    }

    private void SelectTab(BrowserTab tab)
    {
        if (_activeTab?.View is not null) _activeTab.View.Visibility = Visibility.Collapsed;
        _activeTab = tab;
        if (tab.View is not null)
        {
            tab.View.Visibility = Visibility.Visible;
            if (!BrowserHost.Children.Contains(tab.View)) BrowserHost.Children.Add(tab.View);
            AddressBox.Text = tab.View.Source?.ToString() ?? "";
        }
        RefreshTabs();
    }

    private void RefreshTabs()
    {
        TabItems.Items.Refresh();
        if (_activeTab?.View?.CoreWebView2 is { } core)
        {
            BackButtonState(core);
        }
    }

    private void BackButtonState(CoreWebView2 core)
    {
        StatusText.Text = core.CanGoBack || core.CanGoForward
            ? $"{(core.CanGoBack ? "←" : "")}{(core.CanGoForward ? " →" : "")}" : "Search for Windows";
    }

    private static void Navigate(WebView2 view, string input)
    {
        var value = input.Trim();
        if (string.IsNullOrWhiteSpace(value)) return;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            view.Source = uri;
            return;
        }
        if (value.Contains(' ') || !value.Contains('.'))
            view.Source = new Uri("https://www.google.com/search?q=" + Uri.EscapeDataString(value));
        else
            view.Source = new Uri("https://" + value);
    }

    private async void NewTab_Click(object sender, RoutedEventArgs e) => await CreateTabAsync();

    private void SelectTab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is BrowserTab tab) SelectTab(tab);
    }

    private async void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not BrowserTab tab) return;
        var index = _tabs.IndexOf(tab);
        BrowserHost.Children.Remove(tab.View);
        tab.View?.Dispose();
        _tabs.Remove(tab);
        if (_tabs.Count == 0) { await CreateTabAsync(); return; }
        SelectTab(_tabs[Math.Clamp(index - 1, 0, _tabs.Count - 1)]);
    }

    private void AddressBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _activeTab?.View is null) return;
        Navigate(_activeTab.View, AddressBox.Text);
        Keyboard.ClearFocus();
        e.Handled = true;
    }

    private void AddressBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        => AddressBox.SelectAll();

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab?.View?.CoreWebView2?.CanGoBack == true) _activeTab.View.GoBack();
    }

    private void Forward_Click(object sender, RoutedEventArgs e)
    {
        if (_activeTab?.View?.CoreWebView2?.CanGoForward == true) _activeTab.View.GoForward();
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => _activeTab?.View?.Reload();

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        switch (e.Key)
        {
            case Key.L: AddressBox.Focus(); AddressBox.SelectAll(); e.Handled = true; break;
            case Key.T: await CreateTabAsync(); e.Handled = true; break;
            case Key.W:
                if (_activeTab is not null)
                    CloseTab_Click(new Button { Tag = _activeTab }, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.R: _activeTab?.View?.Reload(); e.Handled = true; break;
        }
    }

    private sealed class BrowserTab
    {
        public string Title { get; set; } = "New tab";
        public WebView2? View { get; set; }
        public bool IsSelected => View?.Visibility == Visibility.Visible;
    }
}

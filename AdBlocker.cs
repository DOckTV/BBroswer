namespace Search.Windows;

public static class AdBlocker
{
    // A compact starter denylist. This is not a substitute for EasyList/uBlock filter syntax.
    private static readonly HashSet<string> Hosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "doubleclick.net", "googlesyndication.com", "googleadservices.com",
        "adservice.google.com", "adnxs.com", "adsrvr.org", "taboola.com",
        "outbrain.com", "scorecardresearch.com", "amazon-adsystem.com",
        "adform.net", "criteo.com", "hotjar.com", "branch.io",
        "connect.facebook.net", "analytics.tiktok.com", "bat.bing.com",
        "pixel.quantserve.com", "zedo.com", "moatads.com", "pubmatic.com"
    };

    public static bool ShouldBlock(string address)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) return false;
        var host = uri.Host;
        foreach (var blocked in Hosts)
            if (host.Equals(blocked, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + blocked, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}

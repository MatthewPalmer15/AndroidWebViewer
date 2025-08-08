namespace AndroidWebViewer;

public partial class MainPage : ContentPage
{
    private string TargetUrl { get; } = AppSettings.Get("TargetUrl", "https://example.com");
    private bool EnforceAllowedDomainsOnly => AppSettings.Get("EnforceAllowedDomainsOnly", "true")
                                                        .Equals("true", StringComparison.OrdinalIgnoreCase);

    public MainPage()
    {
        InitializeComponent();
        LoadUrl(TargetUrl);
    }

    private void LoadUrl(string url)
    {
#if ANDROID
        var headers = new Dictionary<string, string> { { "DNT", "1" } };
        SecureWebView.Source = new UrlWebViewSource { Url = url };
#else
        SecureWebView.Source = url;
#endif
    }

    void OnNavigating(object sender, WebNavigatingEventArgs e)
    {
        var url = e.Url ?? "";
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            return; // block non-HTTPS
        }

        if (EnforceAllowedDomainsOnly)
        {
            var destHost = new Uri(url).Host;
            var allowedHost = new Uri(TargetUrl).Host;
            // allow subdomains of the target host
            bool ok = destHost.Equals(allowedHost, StringComparison.OrdinalIgnoreCase)
                   || destHost.EndsWith("." + allowedHost, StringComparison.OrdinalIgnoreCase);
            if (!ok)
            {
                e.Cancel = true;
                return;
            }
        }
    }

    void OnNavigated(object sender, WebNavigatedEventArgs e) { /* no-op */ }
}

public static class AppSettings
{
    private static readonly string EmbeddedName = "appsettings.json";

    public static string Get(string key, string fallback = "")
    {
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync(EmbeddedName).Result;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (dict != null && dict.TryGetValue(key, out var val))
                return val;
        }
        catch { }
        return fallback;
    }
}

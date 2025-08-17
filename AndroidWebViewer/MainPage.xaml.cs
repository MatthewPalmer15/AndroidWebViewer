namespace AndroidWebViewer;

public partial class MainPage : ContentPage
{
    private string TargetUrl { get; } = AppSettings.Get("TargetUrl", "https://example.com");
    private bool EnforceAllowedDomainsOnly => AppSettings.Get("EnforceAllowedDomainsOnly", "true")
                                                        .Equals("true", StringComparison.OrdinalIgnoreCase);

    private readonly HashSet<string> _allowedHosts = new(StringComparer.OrdinalIgnoreCase);

    public MainPage()
    {
        InitializeComponent();

        // Build allow-list (primary + extras)
        var primaryHost = new Uri(TargetUrl).Host;
        _allowedHosts.Add(primaryHost);
        var extras = AppSettings.Get("AdditionalAllowedHosts", "");
        foreach (var h in extras.Split(new[] { ',', ';', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = h.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed)) _allowedHosts.Add(trimmed);
        }

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
            e.Cancel = true; // block non-HTTPS
            return;
        }

        if (EnforceAllowedDomainsOnly)
        {
            var destHost = new Uri(url).Host;

            bool ok = _allowedHosts.Any(allowed =>
                destHost.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                destHost.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase));

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
    private static readonly string DefaultName = "appsettings.json";
    private static Dictionary<string, string>? _cache;

    static AppSettings()
    {
        Load();
    }


    private static string GetBrand()
    {
#if MINE
        return "mine";
#endif

        return "default";
    }

    public static void Load()
    {
        var brand = GetBrand();

        var tried = new List<string>();
        foreach (var name in new[] {
                     string.IsNullOrWhiteSpace(brand) ? null : $"appsettings.{brand}.json",
                     DefaultName
                 }.Where(n => n != null)!)
        {
            tried.Add(name!);
            try
            {
                using var stream = FileSystem.OpenAppPackageFileAsync(name!).Result;
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                _cache = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (_cache != null) return;
            }
            catch { /* try next */ }
        }
        _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static string Get(string key, string fallback = "")
        => (_cache != null && _cache.TryGetValue(key, out var val)) ? val : fallback;

    // Optional helper
    public static string[] GetList(string key)
        => Get(key, "")
            .Split(new[] { ',', ';', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
}
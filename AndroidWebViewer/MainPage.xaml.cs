namespace AndroidWebViewer;

public partial class MainPage : ContentPage
{
    private List<string> TargetUrls { get; } = AppSettings.GetList("TargetUrls");
    private bool EnforceAllowedDomainsOnly => AppSettings.Get("EnforceAllowedDomainsOnly", "true").Equals("true", StringComparison.OrdinalIgnoreCase);

    public MainPage()
    {
        InitializeComponent();

        if (TargetUrls.Count == 0)
            TargetUrls.Add("https://example.com");

        foreach (var url in TargetUrls)
            UrlPicker.Items.Add(url);

        UrlPicker.SelectedIndex = 0;
        LoadUrl(TargetUrls[0]);
    }

    private void LoadUrl(string url)
    {
#if ANDROID
        var headers = new Dictionary<string, string> { { "DNT", "1" } };
        SecureWebView.Source = new UrlWebViewSource { Url = url };
#else
        SecureWebView.Source = url;
#endif
        StatusLabel.Text = url;
    }

    void OnNavigating(object sender, WebNavigatingEventArgs e)
    {
        var url = e.Url ?? "";
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            DisplayAlert("Blocked", "Insecure (non-HTTPS) navigation was blocked.", "OK");
            return;
        }

        if (EnforceAllowedDomainsOnly)
        {
            var allowedHosts = TargetUrls.Select(u => new Uri(u).Host).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var destHost = new Uri(url).Host;
            bool ok = allowedHosts.Any(h => destHost.Equals(h, StringComparison.OrdinalIgnoreCase) || destHost.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));
            if (!ok)
            {
                e.Cancel = true;
                DisplayAlert("Blocked", $"Navigation to external host '{destHost}' is blocked.", "OK");
                return;
            }
        }
    }

    void OnNavigated(object sender, WebNavigatedEventArgs e)
    {
        StatusLabel.Text = e.Url ?? "Loaded";
    }

    void OnUrlSelected(object sender, EventArgs e)
    {
        if (UrlPicker.SelectedIndex >= 0 && UrlPicker.SelectedIndex < UrlPicker.Items.Count)
        {
            var url = UrlPicker.Items[UrlPicker.SelectedIndex];
            LoadUrl(url);
        }
    }

    void OnGoClicked(object sender, EventArgs e)
    {
        if (UrlPicker.SelectedIndex >= 0)
        {
            var url = UrlPicker.Items[UrlPicker.SelectedIndex];
            LoadUrl(url);
        }
    }

    void OnBackClicked(object sender, EventArgs e)
    {
        if (SecureWebView.CanGoBack) SecureWebView.GoBack();
    }

    void OnForwardClicked(object sender, EventArgs e)
    {
        if (SecureWebView.CanGoForward) SecureWebView.GoForward();
    }

    void OnRefreshClicked(object sender, EventArgs e)
    {
        if (SecureWebView?.Source is UrlWebViewSource src)
            LoadUrl(src.Url);
    }
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

    public static List<string> GetList(string key)
    {
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync(EmbeddedName).Result;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(key, out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                return arr.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            }
        }
        catch { }
        return new List<string>();
    }
}

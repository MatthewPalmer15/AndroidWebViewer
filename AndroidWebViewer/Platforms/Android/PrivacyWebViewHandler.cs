using Android.Net.Http;
using Android.Webkit;
using Microsoft.Maui.Handlers;
using WebView = Android.Webkit.WebView;

namespace AndroidWebViewer.Platforms.Android;

public class PrivacyWebViewHandler : WebViewHandler
{
    private static HashSet<string> BlockHosts = new(StringComparer.OrdinalIgnoreCase);
    private static bool Allow3pCookies = true;
    private static bool EnableAdBlocking = true;
    private static string AllowedHost = "";

    protected override void ConnectHandler(WebView platformView)
    {
        base.ConnectHandler(platformView);

        // Load settings
        Allow3pCookies = AppSettings.Get("AllowThirdPartyCookies", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
        EnableAdBlocking = AppSettings.Get("EnableAdBlocking", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
        var targetUrl = AppSettings.Get("TargetUrl", "https://example.com");
        AllowedHost = new Uri(targetUrl).Host;

        // Load blocklist once
        if (BlockHosts.Count == 0)
        {
            try
            {
                using var stream = Microsoft.Maui.Storage.FileSystem.OpenAppPackageFileAsync("blocklist.txt").Result;
                using var reader = new StreamReader(stream);
                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine()?.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
                    set.Add(line);
                }
                BlockHosts = set;
            }
            catch
            {
                // keep empty
            }
        }

        var settings = platformView.Settings;
        settings.JavaScriptEnabled = true;     // needed for most logins
        settings.DomStorageEnabled = true;     // needed for modern auth flows
        settings.DatabaseEnabled = false;      // reduce surface
        settings.SetSupportZoom(false);
        settings.BuiltInZoomControls = false;
        settings.DisplayZoomControls = false;
        settings.SetGeolocationEnabled(false);
        settings.SaveFormData = false;
        settings.MixedContentMode = MixedContentHandling.NeverAllow;
        settings.CacheMode = CacheModes.Default; // allow normal caching for login sessions
        settings.AllowFileAccess = true;
        settings.AllowContentAccess = true;

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            settings.SafeBrowsingEnabled = true;
        }

        // Cookies for logins (persisted by WebView)
        var cm = CookieManager.Instance;
        cm.SetAcceptCookie(true);
        try { CookieManager.Instance.SetAcceptThirdPartyCookies(platformView, Allow3pCookies); } catch { }

        platformView.SetWebViewClient(new HardenedClient(BlockHosts, EnableAdBlocking, AllowedHost));
        platformView.SetWebChromeClient(new WebChromeClient()); // deny runtime permissions by default

        // IMPORTANT: Do NOT clear storage/cache here if you want to stay logged in.
        // platformView.ClearCache(true);                 // <-- leave disabled
        // WebStorage.Instance.DeleteAllData();           // <-- leave disabled
        // CookieManager.Instance.RemoveAllCookies(null); // <-- leave disabled
    }

    private class HardenedClient : WebViewClient
    {
        private readonly HashSet<string> _blockHosts;
        private readonly bool _enableBlock;
        private readonly string _allowedHost;

        public HardenedClient(HashSet<string> blockHosts, bool enableBlock, string allowedHost)
        {
            _blockHosts = blockHosts;
            _enableBlock = enableBlock;
            _allowedHost = allowedHost;
        }

        public override bool ShouldOverrideUrlLoading(WebView view, IWebResourceRequest request)
        {
            var url = request?.Url?.ToString() ?? string.Empty;
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return true;

            // Restrict to the single allowed host (and its subdomains)
            try
            {
                var host = new Uri(url).Host;
                bool ok = host.Equals(_allowedHost, StringComparison.OrdinalIgnoreCase)
                       || host.EndsWith("." + _allowedHost, StringComparison.OrdinalIgnoreCase);
                if (!ok) return true;
            }
            catch { return true; }

            return false;
        }

        public override WebResourceResponse? ShouldInterceptRequest(WebView view, IWebResourceRequest request)
        {
            if (!_enableBlock || request?.Url is null)
                return base.ShouldInterceptRequest(view, request);

            try
            {
                var url = request.Url.ToString();
                if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return Block(); // block any http

                var host = request.Url.Host ?? "";
                if (IsBlockedHost(host))
                    return Block();

                var path = request.Url.Path ?? "";
                if (LooksLikeAdOrTracker(path))
                    return Block();
            }
            catch
            {
                return Block();
            }

            return base.ShouldInterceptRequest(view, request);
        }

        private bool IsBlockedHost(string host)
        {
            if (string.IsNullOrEmpty(host)) return true;
            var parts = host.Split('.');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var suffix = string.Join(".", parts.Skip(i));
                if (_blockHosts.Contains(suffix)) return true;
            }
            return false;
        }

        private bool LooksLikeAdOrTracker(string path)
        {
            var p = path.ToLowerInvariant();
            if (p.Contains("/ads/") || p.Contains("/adserver/") || p.Contains("/advert") || p.Contains("/banner"))
                return true;
            if (p.Contains("gpt.js") || p.Contains("adscript") || p.Contains("/analytics") || p.Contains("analytics.js"))
                return true;
            if (p.Contains("/measure") || p.Contains("/track") || p.Contains("/pixel"))
                return true;
            return false;
        }

        private WebResourceResponse Block()
        {
            return new WebResourceResponse("text/plain", "utf-8", 204, "No Content",
                new Dictionary<string, string>(), new MemoryStream());
        }

        public override void OnReceivedSslError(WebView view, SslErrorHandler handler, SslError error)
        {
            handler?.Cancel();
        }
    }
}

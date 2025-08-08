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
    private static HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase);

    protected override void ConnectHandler(WebView platformView)
    {
        base.ConnectHandler(platformView);

        // Load settings
        Allow3pCookies = AppSettings.Get("AllowThirdPartyCookies", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
        EnableAdBlocking = AppSettings.Get("EnableAdBlocking", "true").Equals("true", StringComparison.OrdinalIgnoreCase);
        var allowedUrls = AppSettings.GetList("TargetUrls") ?? new List<string>();
        AllowedHosts = allowedUrls.Where(u => !string.IsNullOrWhiteSpace(u))
                                  .Select(u => new Uri(u).Host)
                                  .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
        settings.JavaScriptEnabled = true;     // needed for most login flows
        settings.DomStorageEnabled = true;     // modern sites
        settings.DatabaseEnabled = false;      // reduce local persistence
        settings.SetSupportZoom(false);
        settings.BuiltInZoomControls = false;
        settings.DisplayZoomControls = false;
        settings.SetGeolocationEnabled(false);
        settings.SaveFormData = false;
        settings.MixedContentMode = MixedContentHandling.NeverAllow;
        settings.CacheMode = CacheModes.NoCache;

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            settings.SafeBrowsingEnabled = true;
        }

        // Cookies for logins (incl. 3rd-party toggle)
        var cm = CookieManager.Instance;
        cm.SetAcceptCookie(true);
        try { CookieManager.Instance.SetAcceptThirdPartyCookies(platformView, Allow3pCookies); } catch { }

        platformView.SetWebViewClient(new HardenedClient(BlockHosts, EnableAdBlocking, AllowedHosts));
        platformView.SetWebChromeClient(new HardenedChromeClient()); // deny runtime permissions by default
        platformView.ClearCache(true);
        WebStorage.Instance.DeleteAllData();
    }

    private class HardenedChromeClient : WebChromeClient
    {
        public override void OnPermissionRequest(PermissionRequest request)
        {
            // Deny camera/mic/geo prompts from pages by default.
            request?.Deny();
        }
    }

    private class HardenedClient : WebViewClient
    {
        private readonly HashSet<string> _blockHosts;
        private readonly bool _enableBlock;
        private readonly HashSet<string> _allowedHosts;

        public HardenedClient(HashSet<string> blockHosts, bool enableBlock, HashSet<string> allowedHosts)
        {
            _blockHosts = blockHosts;
            _enableBlock = enableBlock;
            _allowedHosts = allowedHosts;
        }

        public override bool ShouldOverrideUrlLoading(WebView view, IWebResourceRequest request)
        {
            var url = request?.Url?.ToString() ?? string.Empty;
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return true;

            // Restrict to allowed hosts (and subdomains)
            try
            {
                var host = new Uri(url).Host;
                bool ok = _allowedHosts.Any(h => host.Equals(h, StringComparison.OrdinalIgnoreCase) || host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));
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
                    return Block(); // no http ever

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
            if (p.Contains("gpt.js") || p.Contains("adscript") || p.Contains("analytics.js"))
                return true;
            if (p.Contains("/analytics") || p.Contains("/measure") || p.Contains("/track") || p.Contains("/pixel"))
                return true;
            return false;
        }

        private WebResourceResponse Block()
        {
            return new WebResourceResponse("text/plain", "utf-8", 204, "No Content", new Dictionary<string, string>(), new MemoryStream());
        }

        public override void OnReceivedSslError(WebView view, SslErrorHandler handler, SslError error)
        {
            handler?.Cancel(); // never bypass SSL errors
        }
    }
}

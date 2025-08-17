using Android.App;
using Android.Net.Http;
using Android.Webkit;
using Microsoft.Maui.Handlers;
using Application = Microsoft.Maui.Controls.Application;
using WebView = Android.Webkit.WebView;

namespace AndroidWebViewer.Platforms.Android
{
    public class PrivacyWebViewHandler : WebViewHandler
    {
        private static HashSet<string> BlockHosts = new(StringComparer.OrdinalIgnoreCase);
        private static bool Allow3pCookies = true;
        private static bool EnableAdBlocking = true;

        // CHANGED: support multiple allowed hosts
        private static HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase);

        // Expose current ChromeClient so MainActivity can forward activity results
        public static FileChooserWebChromeClient? CurrentChromeClient { get; private set; }

        protected override void ConnectHandler(WebView platformView)
        {
            base.ConnectHandler(platformView);

            // Load settings
            Allow3pCookies = AppSettings.Get("AllowThirdPartyCookies", "true")
                                         .Equals("true", StringComparison.OrdinalIgnoreCase);
            EnableAdBlocking = AppSettings.Get("EnableAdBlocking", "true")
                                          .Equals("true", StringComparison.OrdinalIgnoreCase);

            var targetUrl = AppSettings.Get("TargetUrl", "https://example.com");
            var primaryHost = new Uri(targetUrl).Host;

            // Build allow-list: primary + optional extras from appsettings
            AllowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { primaryHost };
            var extras = AppSettings.Get("AdditionalAllowedHosts", "");
            foreach (var h in extras.Split(new[] { ',', ';', ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = h.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed)) AllowedHosts.Add(trimmed);
            }

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
                catch { /* keep empty */ }
            }

            var settings = platformView.Settings;
            settings.JavaScriptEnabled = true;     // needed for most logins
            settings.DomStorageEnabled = true;     // modern auth flows
            settings.DatabaseEnabled = false;
            settings.SetSupportZoom(false);
            settings.BuiltInZoomControls = false;
            settings.DisplayZoomControls = false;
            settings.SetGeolocationEnabled(false);
            settings.SaveFormData = false;
            settings.MixedContentMode = MixedContentHandling.NeverAllow;
            settings.CacheMode = CacheModes.Default;
            settings.AllowFileAccess = true;
            settings.AllowContentAccess = true;
            if (OperatingSystem.IsAndroidVersionAtLeast(26))
                settings.SafeBrowsingEnabled = true;
            settings.MediaPlaybackRequiresUserGesture = true;

            // Cookies for logins (persist across restarts)
            var cm = CookieManager.Instance;
            cm.SetAcceptCookie(true);
            try { CookieManager.Instance.SetAcceptThirdPartyCookies(platformView, Allow3pCookies); } catch { }

            // Hardened client with allow-list + ad/tracker blocking
            platformView.SetWebViewClient(new HardenedClient(BlockHosts, EnableAdBlocking, AllowedHosts));

            // CHANGED: use our file-chooser-enabled ChromeClient
            var activity = Platform.CurrentActivity ?? (Application.Current?.Handler?.MauiContext?.Services.GetService(typeof(Activity)) as Activity);
            var chrome = new FileChooserWebChromeClient(activity!);
            platformView.SetWebChromeClient(chrome);
            CurrentChromeClient = chrome;

            // Do NOT clear storage/cache here
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

                // Allow main-frame navigations to allow-listed hosts (incl. subdomains)
                try
                {
                    var host = new Uri(url).Host;
                    if (request?.IsForMainFrame == true && !IsAllowedHost(host))
                        return true;
                }
                catch { return true; }

                return false; // let WebView load it
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

            private bool IsAllowedHost(string host)
            {
                foreach (var allowed in _allowedHosts)
                {
                    if (host.Equals(allowed, StringComparison.OrdinalIgnoreCase) ||
                        host.EndsWith("." + allowed, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                return false;
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
}

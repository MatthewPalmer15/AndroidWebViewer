using global::Android.OS;
using global::Android.Webkit;
using Microsoft.Maui.Handlers;
using Intent = Android.Content.Intent;
using Message = Android.OS.Message;
using Uri = Android.Net.Uri;
using WebView = Android.Webkit.WebView;

namespace AndroidWebViewer.Platforms.Android.Web;

public class MyAndroidWebViewHandler : WebViewHandler
{
    protected override void ConnectHandler(WebView platformView)
    {
        base.ConnectHandler(platformView);

        var s = platformView.Settings;
        s.JavaScriptEnabled = true;
        s.DomStorageEnabled = true;
        s.DatabaseEnabled = true;
        s.SetSupportMultipleWindows(true);
        s.JavaScriptCanOpenWindowsAutomatically = true;
        s.MediaPlaybackRequiresUserGesture = false;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            s.MixedContentMode = MixedContentHandling.AlwaysAllow;

        platformView.SetWebViewClient(new InsideAppWebViewClient());
        platformView.SetWebChromeClient(new ChooserChromeClient());

        var cm = CookieManager.Instance;
        cm.SetAcceptCookie(true);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            cm.SetAcceptThirdPartyCookies(platformView, true);
    }

    class InsideAppWebViewClient : WebViewClient
    {
        public override bool ShouldOverrideUrlLoading(WebView view, IWebResourceRequest request)
        {
            var url = request?.Url?.ToString() ?? "";
            if (url.StartsWith("http"))
            {
                return false;
            }

            try
            {
                var intent = new Intent(Intent.ActionView, request.Url);
                view.Context.StartActivity(intent);
            }
            catch { /* ignore */ }
            return true;
        }
    }
    class ChooserChromeClient : WebChromeClient
    {
        IValueCallback? _fileCallback;

        public override bool OnShowFileChooser(WebView webView,
            IValueCallback filePathCallback, FileChooserParams fileChooserParams)
        {
            _fileCallback = filePathCallback;

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    var results = await FilePicker.Default.PickMultipleAsync();
                    var uris = results?
                        .Select(r => Uri.FromFile(new Java.IO.File(r.FullPath)))
                        .ToArray();
                    _fileCallback?.OnReceiveValue(uris);
                }
                catch { _fileCallback?.OnReceiveValue(null); }
            });

            return true;
        }

        public override bool OnCreateWindow(WebView view, bool isDialog, bool isUserGesture, Message resultMsg)
        {
            var transport = (WebView.WebViewTransport)resultMsg.Obj;
            transport.WebView = view;
            resultMsg.SendToTarget();
            return true;
        }
    }
}

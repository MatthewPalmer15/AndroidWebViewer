using Android.OS;
using Android.Webkit;
using AndroidWebViewer.Platforms.Android.Clients;
using Microsoft.Maui.Handlers;
using WebView = Android.Webkit.WebView;

namespace AndroidWebViewer.Platforms.Android.Handlers;

public class NormalWebViewHandler : WebViewHandler
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

        platformView.SetWebViewClient(new NormalWebViewClient());
        platformView.SetWebChromeClient(new NormalWebChromeClient());

        var cm = CookieManager.Instance;
        cm.SetAcceptCookie(true);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
            cm.SetAcceptThirdPartyCookies(platformView, true);
    }
}
using Android.Content;
using Android.Webkit;
using WebView = Android.Webkit.WebView;

namespace AndroidWebViewer.Platforms.Android.Clients;

internal class NormalWebViewClient : WebViewClient
{
    public override bool ShouldOverrideUrlLoading(WebView view, IWebResourceRequest request)
    {
        var url = request?.Url?.ToString() ?? "";
        if (url.StartsWith("http")) return false;

        try
        {
            var intent = new Intent(Intent.ActionView, request.Url);
            view.Context.StartActivity(intent);
        }
        catch
        {
            /* ignore */
        }

        return true;
    }
}
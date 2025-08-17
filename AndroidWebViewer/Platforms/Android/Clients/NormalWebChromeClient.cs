using Android.Webkit;
using File = Java.IO.File;
using Message = Android.OS.Message;
using Uri = Android.Net.Uri;
using WebView = Android.Webkit.WebView;

namespace AndroidWebViewer.Platforms.Android.Clients;

public class NormalWebChromeClient : WebChromeClient
{
    private IValueCallback? _fileCallback;

    public override bool OnShowFileChooser(WebView webView, IValueCallback filePathCallback,
        FileChooserParams fileChooserParams)
    {
        _fileCallback = filePathCallback;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var results = await FilePicker.Default.PickMultipleAsync();
                var uris = results?.Select(r => Uri.FromFile(new File(r.FullPath))).ToArray();
                _fileCallback?.OnReceiveValue(uris);
            }
            catch
            {
                _fileCallback?.OnReceiveValue(null);
            }
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
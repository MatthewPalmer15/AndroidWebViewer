using Android.App;
using Android.Content;
using Android.Webkit;
using WebView = Android.Webkit.WebView;

namespace AndroidWebViewer.Platforms.Android.Clients;

public class PrivacyWebChromeClient(Activity activity) : WebChromeClient
{
    private const int RequestCode = 5173;
    private IValueCallback? _filePathCallback;

    public override bool OnShowFileChooser(WebView webView, IValueCallback filePathCallback,
        FileChooserParams fileChooserParams)
    {
        _filePathCallback?.OnReceiveValue(null);
        _filePathCallback = filePathCallback;

        Intent? intent = null;

        try
        {
            intent = fileChooserParams?.CreateIntent();
        }
        catch (ActivityNotFoundException)
        {
        }

        if (intent == null)
        {
            intent = new Intent(Intent.ActionOpenDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("*/*");
        }

        var allowMultiple = fileChooserParams?.Mode == ChromeFileChooserMode.OpenMultiple;
        intent.PutExtra(Intent.ExtraAllowMultiple, allowMultiple);

        try
        {
            activity.StartActivityForResult(intent, RequestCode);
            return true;
        }
        catch (ActivityNotFoundException)
        {
            _filePathCallback?.OnReceiveValue(null);
            _filePathCallback = null;
            return false;
        }
    }

    public void HandleActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != RequestCode || _filePathCallback == null)
            return;

        var results = FileChooserParams.ParseResult((int)resultCode, data);
        _filePathCallback.OnReceiveValue(results);
        _filePathCallback = null;
    }
}
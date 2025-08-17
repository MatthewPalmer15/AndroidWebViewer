using Android.App;
using Android.Content;
using Android.Webkit;
using WebView = Android.Webkit.WebView;

namespace AndroidWebViewer.Platforms.Android
{
    // Handles <input type="file"> for the Android WebView
    public class FileChooserWebChromeClient : WebChromeClient
    {
        public const int RequestCode = 5173;

        private readonly Activity _activity;
        private IValueCallback? _filePathCallback;

        public FileChooserWebChromeClient(Activity activity)
        {
            _activity = activity;
        }

        public override bool OnShowFileChooser(WebView webView,
                                               IValueCallback filePathCallback,
                                               FileChooserParams fileChooserParams)
        {
            // Replace any outstanding callback
            _filePathCallback?.OnReceiveValue(null);
            _filePathCallback = filePathCallback;

            Intent? intent = null;

            // Prefer the system-provided intent when available
            try
            {
                intent = fileChooserParams?.CreateIntent();
            }
            catch (ActivityNotFoundException) { }

            // Fallback to ACTION_OPEN_DOCUMENT (Storage Access Framework) – no runtime permission needed
            if (intent == null)
            {
                intent = new Intent(Intent.ActionOpenDocument);
                intent.AddCategory(Intent.CategoryOpenable);
                intent.SetType("*/*");
            }

            // Respect multiple selection when requested
            bool allowMultiple = fileChooserParams?.Mode == ChromeFileChooserMode.OpenMultiple;
            intent.PutExtra(Intent.ExtraAllowMultiple, allowMultiple);

            try
            {
                _activity.StartActivityForResult(intent, RequestCode);
                return true;
            }
            catch (ActivityNotFoundException)
            {
                // No picker available — fail gracefully
                _filePathCallback?.OnReceiveValue(null);
                _filePathCallback = null;
                return false;
            }
        }

        // Forward results from MainActivity
        public void HandleActivityResult(int requestCode, Result resultCode, Intent? data)
        {
            if (requestCode != RequestCode || _filePathCallback == null)
                return;

            var results = WebChromeClient.FileChooserParams.ParseResult((int)resultCode, data);
            _filePathCallback.OnReceiveValue(results);
            _filePathCallback = null;
        }
    }
}

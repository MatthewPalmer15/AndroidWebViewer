using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Webkit;
using AndroidWebViewer.Platforms.Android.Handlers;
using WebView = Android.Webkit.WebView;

namespace AndroidWebViewer.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // Disable for release if you prefer
        WebView.SetWebContentsDebuggingEnabled(false);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        PrivacyWebViewHandler.CurrentChromeClient?.HandleActivityResult(requestCode, resultCode, data);
    }

    protected override void OnStop()
    {
        base.OnStop();
        try
        {
            CookieManager.Instance.Flush();
        }
        catch
        {
        }
    }
}
using AndroidWebViewer.Platforms.Android.Handlers;

namespace AndroidWebViewer;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
            builder.Logging.AddDebug();
#endif

#if ANDROID
        builder.ConfigureMauiHandlers(handlers =>
        {
            handlers.AddHandler(typeof(WebView), typeof(NormalWebViewHandler));
        });
#endif

        return builder.Build();
    }
}
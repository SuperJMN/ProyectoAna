using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace EvaluacionesApp.Android;

[Activity(
    Label = "Evaluaciones",
    Theme = "@style/MyTheme.Splash",
    Icon = "@mipmap/appicon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}

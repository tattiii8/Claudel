using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Claudel.Services;

namespace Claudel;

public partial class App : Application
{
    public static EntraAuthenticationService?
        AuthenticationService
    {
        get;
        set;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService =
                new SettingsService();

            var settings =
                await settingsService.LoadAsync();

            var loginWindow =
                new LoginWindow(
                    settingsService,
                    settings);

            desktop.MainWindow =
                loginWindow;

            loginWindow.Closed +=
                (_, _) =>
                {
                    if (App.AuthenticationService != null)
                    {
                        desktop.MainWindow =
                            new MainWindow();

                        desktop.MainWindow.Show();
                    }
                    else
                    {
                        desktop.Shutdown();
                    }
                };

            loginWindow.Show();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Claudel.Models;
using Claudel.Services;
using Microsoft.Identity.Client;

namespace Claudel;

public partial class LoginWindow : Window
{
    private readonly SettingsService _settingsService;

    public AppSettings Settings { get; private set; }

    public AuthenticationResult? AuthenticationResult
    {
        get;
        private set;
    }

    public LoginWindow(
        SettingsService settingsService,
        AppSettings settings)
    {
        InitializeComponent();

        _settingsService = settingsService;
        Settings = settings;

        TenantIdTextBox.Text =
            Settings.EntraId.TenantId;

        ClientIdTextBox.Text =
            Settings.EntraId.ClientId;
    }

    private EntraIdSettings ReadEntraSettings()
    {
        return new EntraIdSettings
        {
            TenantId =
                TenantIdTextBox.Text?.Trim() ?? "",

            ClientId =
                ClientIdTextBox.Text?.Trim() ?? ""
        };
    }

    private async void SignIn_Click(
        object? sender,
        RoutedEventArgs e)
    {
        try
        {
            var entraSettings =
                ReadEntraSettings();

            if (string.IsNullOrWhiteSpace(
                    entraSettings.TenantId))
            {
                await ShowMessageAsync(
                    "Tenant ID is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    entraSettings.ClientId))
            {
                await ShowMessageAsync(
                    "Client ID is required.");

                return;
            }

            SignInButton.IsEnabled = false;

            StatusTextBlock.Text =
                "Microsoft Entra IDに接続しています...";

            Settings.EntraId =
                entraSettings;

            await _settingsService.SaveAsync(
                Settings);

            var authenticationService =
                new EntraAuthenticationService(
                    Settings.EntraId);

            AuthenticationResult =
                await authenticationService.SignInAsync();

            App.AuthenticationService =
                authenticationService;

            StatusTextBlock.Text =
                $"サインインしました: {AuthenticationResult.Account.Username}";

            await Task.Delay(300);

            Close(true);
        }
        catch (MsalException ex)
        {
            StatusTextBlock.Text =
                "Microsoft Entra IDへのサインインに失敗しました。";

            await ShowMessageAsync(
                $"Entra ID sign-in failed.\n\n{ex.Message}");
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text =
                "サインインに失敗しました。";

            await ShowMessageAsync(
                $"Sign-in failed.\n\n{ex.Message}");
        }
        finally
        {
            SignInButton.IsEnabled = true;
        }
    }

    private void Cancel_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Close(false);
    }

    private async Task ShowMessageAsync(
        string message)
    {
        var dialog =
            new Window
            {
                Title = "Claudel",
                Width = 500,
                Height = 220,

                Content =
                    new TextBlock
                    {
                        Text = message,

                        TextWrapping =
                            Avalonia.Media.TextWrapping.Wrap,

                        Margin =
                            new Avalonia.Thickness(20)
                    }
            };

        await dialog.ShowDialog(this);
    }
}
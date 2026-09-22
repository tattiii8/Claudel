using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Claudel.Data;
using Claudel.Models;
using Claudel.Services;

namespace Claudel;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;

    public AppSettings Settings { get; private set; }

    public SettingsWindow(
        SettingsService settingsService,
        AppSettings settings)
    {
        InitializeComponent();

        _settingsService =
            settingsService;

        Settings =
            settings;

        LoadSettings();
    }

    private void LoadSettings()
    {
        MySqlHostTextBox.Text =
            Settings.MySql.Host;

        MySqlPortTextBox.Text =
            Settings.MySql.Port.ToString();

        MySqlDatabaseTextBox.Text =
            Settings.MySql.Database;

        MySqlUserTextBox.Text =
            Settings.MySql.User;

        MySqlPasswordTextBox.Text =
            Settings.MySql.Password;

        S3AccessKeyIdTextBox.Text =
            Settings.S3.AccessKeyId;

        S3SecretAccessKeyTextBox.Text =
            Settings.S3.SecretAccessKey;

        S3RegionTextBox.Text =
            Settings.S3.Region;

        S3BucketTextBox.Text =
            Settings.S3.Bucket;

        RedmineUrlTextBox.Text =
            Settings.Redmine.Url;

        RedmineApiKeyTextBox.Text =
            Settings.Redmine.ApiKey;

        RedmineProjectIdTextBox.Text =
            Settings.Redmine.ProjectId;

        RedmineTrackerIdTextBox.Text =
            Settings.Redmine.TrackerId.ToString();

        KavitaUrlTextBox.Text =
            Settings.Kavita.Url;

        KavitaApiKeyTextBox.Text =
            Settings.Kavita.ApiKey;

        KavitaLibraryIdTextBox.Text =
            Settings.Kavita.LibraryId.ToString();

        KavitaSftpHostTextBox.Text =
            Settings.KavitaSftp.Host;

        KavitaSftpPortTextBox.Text =
            Settings.KavitaSftp.Port.ToString();

        KavitaSftpUserTextBox.Text =
            Settings.KavitaSftp.User;

        KavitaSftpPrivateKeyPathTextBox.Text =
            Settings.KavitaSftp.PrivateKeyPath;

        KavitaSftpRemotePathTextBox.Text =
            Settings.KavitaSftp.RemotePath;
    }

    private AppSettings ReadSettings()
    {
        var mysqlPort =
            3306;

        if (int.TryParse(
                MySqlPortTextBox.Text,
                out var parsedMysqlPort))
        {
            mysqlPort =
                parsedMysqlPort;
        }

        var redmineTrackerId =
            5;

        if (int.TryParse(
                RedmineTrackerIdTextBox.Text,
                out var parsedTrackerId))
        {
            redmineTrackerId =
                parsedTrackerId;
        }

        var kavitaLibraryId =
            0;

        if (int.TryParse(
                KavitaLibraryIdTextBox.Text,
                out var parsedLibraryId))
        {
            kavitaLibraryId =
                parsedLibraryId;
        }

        var kavitaSftpPort =
            22;

        if (int.TryParse(
                KavitaSftpPortTextBox.Text,
                out var parsedSftpPort))
        {
            kavitaSftpPort =
                parsedSftpPort;
        }

        return new AppSettings
        {
            MySql =
                new MySqlSettings
                {
                    Host =
                        MySqlHostTextBox.Text?.Trim()
                        ?? "",

                    Port =
                        mysqlPort,

                    Database =
                        MySqlDatabaseTextBox.Text?.Trim()
                        ?? "",

                    User =
                        MySqlUserTextBox.Text?.Trim()
                        ?? "",

                    Password =
                        MySqlPasswordTextBox.Text
                        ?? ""
                },

            S3 =
                new S3Settings
                {
                    AccessKeyId =
                        S3AccessKeyIdTextBox.Text?.Trim()
                        ?? "",

                    SecretAccessKey =
                        S3SecretAccessKeyTextBox.Text
                        ?? "",

                    Region =
                        S3RegionTextBox.Text?.Trim()
                        ?? "",

                    Bucket =
                        S3BucketTextBox.Text?.Trim()
                        ?? ""
                },

            Redmine =
                new RedmineSettings
                {
                    Url =
                        RedmineUrlTextBox.Text?.Trim()
                        ?? "",

                    ApiKey =
                        RedmineApiKeyTextBox.Text
                        ?? "",

                    ProjectId =
                        RedmineProjectIdTextBox.Text?.Trim()
                        ?? "",

                    TrackerId =
                        redmineTrackerId
                },

            Kavita =
                new KavitaSettings
                {
                    Url =
                        KavitaUrlTextBox.Text?.Trim()
                        ?? "",

                    ApiKey =
                        KavitaApiKeyTextBox.Text
                        ?? "",

                    LibraryId =
                        kavitaLibraryId
                },

            KavitaSftp =
                new KavitaSftpSettings
                {
                    Host =
                        KavitaSftpHostTextBox.Text?.Trim()
                        ?? "",

                    Port =
                        kavitaSftpPort,

                    User =
                        KavitaSftpUserTextBox.Text?.Trim()
                        ?? "",

                    PrivateKeyPath =
                        KavitaSftpPrivateKeyPathTextBox.Text?.Trim()
                        ?? "",

                    RemotePath =
                        string.IsNullOrWhiteSpace(
                            KavitaSftpRemotePathTextBox.Text)
                            ? "/opt/kavita/data/documents"
                            : KavitaSftpRemotePathTextBox.Text.Trim()
                },

            EntraId =
                Settings.EntraId
        };
    }

    private async void TestMySql_Click(
        object? sender,
        RoutedEventArgs e)
    {
        try
        {
            var settings =
                ReadSettings();

            if (string.IsNullOrWhiteSpace(
                    settings.MySql.Host))
            {
                await ShowMessageAsync(
                    "MySQL Host is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.MySql.Database))
            {
                await ShowMessageAsync(
                    "MySQL Database is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.MySql.User))
            {
                await ShowMessageAsync(
                    "MySQL User is required.");

                return;
            }

            if (settings.MySql.Port <= 0 ||
                settings.MySql.Port > 65535)
            {
                await ShowMessageAsync(
                    "MySQL Port is invalid.");

                return;
            }

            var connectionString =
                BuildConnectionString(
                    settings);

            var database =
                new Database(
                    connectionString);

            await using var connection =
                await database.OpenConnectionAsync();

            await ShowMessageAsync(
                "MySQL connection succeeded.");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(
                $"MySQL connection failed.\n\n" +
                ex.Message);
        }
    }

    private async void TestRedmine_Click(
        object? sender,
        RoutedEventArgs e)
    {
        try
        {
            var settings =
                ReadSettings();

            if (string.IsNullOrWhiteSpace(
                    settings.Redmine.Url))
            {
                await ShowMessageAsync(
                    "Redmine URL is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.Redmine.ApiKey))
            {
                await ShowMessageAsync(
                    "Redmine API Access Key is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.Redmine.ProjectId))
            {
                await ShowMessageAsync(
                    "Redmine Project ID is required.");

                return;
            }

            if (settings.Redmine.TrackerId <= 0)
            {
                await ShowMessageAsync(
                    "Redmine Tracker ID is invalid.");

                return;
            }

            TestRedmineButton.IsEnabled =
                false;

            var service =
                new RedmineService(
                    settings.Redmine);

            await service.TestConnectionAsync();

            await ShowMessageAsync(
                "Redmine connection succeeded.");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(
                $"Redmine connection failed.\n\n" +
                ex.Message);
        }
        finally
        {
            TestRedmineButton.IsEnabled =
                true;
        }
    }

    private async void TestKavita_Click(
        object? sender,
        RoutedEventArgs e)
    {
        try
        {
            var settings =
                ReadSettings();

            if (string.IsNullOrWhiteSpace(
                    settings.Kavita.Url))
            {
                await ShowMessageAsync(
                    "Kavita URL is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.Kavita.ApiKey))
            {
                await ShowMessageAsync(
                    "Kavita API Key is required.");

                return;
            }

            TestKavitaButton.IsEnabled =
                false;

            var service =
                new KavitaService(
                    settings.Kavita);

            await service.TestConnectionAsync();

            await ShowMessageAsync(
                "Kavita connection succeeded.");
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(
                $"Kavita connection failed.\n\n" +
                ex.Message);
        }
        finally
        {
            TestKavitaButton.IsEnabled =
                true;
        }
    }

    private async void Save_Click(
        object? sender,
        RoutedEventArgs e)
    {
        try
        {
            var settings =
                ReadSettings();

            if (string.IsNullOrWhiteSpace(
                    settings.MySql.Host))
            {
                await ShowMessageAsync(
                    "MySQL Host is required.");

                return;
            }

            if (settings.MySql.Port <= 0 ||
                settings.MySql.Port > 65535)
            {
                await ShowMessageAsync(
                    "MySQL Port is invalid.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.MySql.Database))
            {
                await ShowMessageAsync(
                    "MySQL Database is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.MySql.User))
            {
                await ShowMessageAsync(
                    "MySQL User is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.S3.AccessKeyId))
            {
                await ShowMessageAsync(
                    "S3 Access Key ID is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.S3.SecretAccessKey))
            {
                await ShowMessageAsync(
                    "S3 Secret Access Key is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.S3.Region))
            {
                await ShowMessageAsync(
                    "S3 Region is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.S3.Bucket))
            {
                await ShowMessageAsync(
                    "S3 Bucket is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.Redmine.Url))
            {
                await ShowMessageAsync(
                    "Redmine URL is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.Redmine.ApiKey))
            {
                await ShowMessageAsync(
                    "Redmine API Access Key is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.Redmine.ProjectId))
            {
                await ShowMessageAsync(
                    "Redmine Project ID is required.");

                return;
            }

            if (settings.Redmine.TrackerId <= 0)
            {
                await ShowMessageAsync(
                    "Redmine Tracker ID is invalid.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.Kavita.Url))
            {
                await ShowMessageAsync(
                    "Kavita URL is required.");

                return;
            }

            if (string.IsNullOrWhiteSpace(
                    settings.Kavita.ApiKey))
            {
                await ShowMessageAsync(
                    "Kavita API Key is required.");

                return;
            }

            if (settings.Kavita.LibraryId <= 0)
            {
                await ShowMessageAsync(
                    "Kavita Library ID is invalid.");

                return;
            }

            /*
             * Kavita SFTP settings are intentionally optional.
             *
             * Document registration and Kavita metadata
             * linking do not require SFTP configuration.
             */
            if (settings.KavitaSftp.Port <= 0 ||
                settings.KavitaSftp.Port > 65535)
            {
                await ShowMessageAsync(
                    "Kavita SFTP Port is invalid.");

                return;
            }

            await _settingsService.SaveAsync(
                settings);

            Settings =
                settings;

            Close(true);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(
                $"Failed to save settings.\n\n" +
                ex.Message);
        }
    }

    private void Cancel_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Close(false);
    }

    private static string BuildConnectionString(
        AppSettings settings)
    {
        return
            $"Server={settings.MySql.Host};" +
            $"Port={settings.MySql.Port};" +
            $"Database={settings.MySql.Database};" +
            $"User ID={settings.MySql.User};" +
            $"Password={settings.MySql.Password};";
    }

    private async Task ShowMessageAsync(
        string message)
    {
        var dialog =
            new Window
            {
                Title =
                    "Settings",

                Width =
                    500,

                Height =
                    220,

                Content =
                    new TextBlock
                    {
                        Text =
                            message,

                        TextWrapping =
                            Avalonia.Media.TextWrapping.Wrap,

                        Margin =
                            new Avalonia.Thickness(20)
                    }
            };

        await dialog.ShowDialog(
            this);
    }
}
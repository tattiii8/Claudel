using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Claudel.Data;
using Claudel.Models;
using Claudel.Repositories;
using Claudel.Services;

namespace Claudel;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService;

    private DocumentRepository? _repository;

    private AppSettings? _settings;

    private CoverCacheService? _coverCacheService;

    public ObservableCollection<Document> Documents { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        _settingsService =
            new SettingsService();

        DataContext = this;

        Opened += MainWindow_Opened;
    }

    private async void MainWindow_Opened(
        object? sender,
        EventArgs e)
    {
        await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            _settings =
                await _settingsService.LoadAsync();

            if (!_settingsService.Exists())
            {
                await OpenSettingsAsync();

                if (_repository == null)
                {
                    return;
                }
            }
            else
            {
                CreateRepository();
            }

            await LoadDocumentsAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
    }

    private void CreateRepository()
    {
        if (_settings == null)
        {
            return;
        }

        var connectionString =
            BuildConnectionString(_settings);

        var database =
            new Database(connectionString);

        _repository =
            new DocumentRepository(database);

        _coverCacheService =
            new CoverCacheService(_settings);
    }

    private async Task OpenSettingsAsync()
    {
        if (_settings == null)
        {
            _settings = new AppSettings();
        }

        var window =
            new SettingsWindow(
                _settingsService,
                _settings);

        var result =
            await window.ShowDialog<bool?>(this);

        if (result == true)
        {
            _settings =
                window.Settings;

            CreateRepository();
        }
    }

    private async void SearchTextBox_TextChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        if (_repository == null)
        {
            return;
        }

        if (sender is not TextBox textBox)
        {
            return;
        }

        await LoadDocumentsAsync(
            textBox.Text ?? "");
    }

    private async void DocumentListBox_DoubleTapped(
        object? sender,
        TappedEventArgs e)
    {
        if (DocumentListBox.SelectedItem
            is not Document document)
        {
            return;
        }

        if (_settings == null)
        {
            await ShowErrorAsync(
                "Settings are not configured.");

            return;
        }

        if (_repository == null)
        {
            await ShowErrorAsync(
                "MySQL is not configured.");

            return;
        }

        var window =
            new DocumentWindow(
                document,
                _settings,
                _repository);

        await window.ShowDialog(this);

        await LoadDocumentsAsync(
            SearchTextBox.Text ?? "");
    }

    private async void NewDocument_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (_repository == null)
        {
            await ShowErrorAsync(
                "MySQL is not configured.");

            return;
        }

        if (_settings == null)
        {
            await ShowErrorAsync(
                "Settings are not configured.");

            return;
        }

        var window =
            new NewDocumentWindow(
                _repository,
                _settings);

        await window.ShowDialog(this);

        await LoadDocumentsAsync(
            SearchTextBox.Text ?? "");
    }

    private async void Settings_Click(
        object? sender,
        RoutedEventArgs e)
    {
        await OpenSettingsAsync();

        if (_repository != null)
        {
            await LoadDocumentsAsync(
                SearchTextBox.Text ?? "");
        }
    }

    private async Task LoadDocumentsAsync(
        string keyword = "")
    {
        if (_repository == null)
        {
            return;
        }

        try
        {
            var documents =
                await _repository.SearchAsync(keyword);

            Documents.Clear();

            foreach (var document in documents)
            {
                if (_coverCacheService != null &&
                    !string.IsNullOrWhiteSpace(
                        document.CoverS3Key))
                {
                    try
                    {
                        document.CoverImage =
                            await _coverCacheService
                                .GetCoverAsync(document);
                    }
                    catch
                    {
                        document.CoverImage = null;
                    }
                }

                Documents.Add(document);
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
        }
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

    private async Task ShowErrorAsync(
        string message)
    {
        var dialog =
            new Window
            {
                Title = "Error",
                Width = 500,
                Height = 200,

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
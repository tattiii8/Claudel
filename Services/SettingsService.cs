using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Claudel.Models;

namespace Claudel.Services;

public class SettingsService
{
    private readonly string _settingsPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public SettingsService()
    {
        var applicationDataPath =
            OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData)
                : Environment.GetFolderPath(
                    Environment.SpecialFolder.ApplicationData);

        var directory =
            Path.Combine(
                applicationDataPath,
                "Claudel");

        Directory.CreateDirectory(directory);

        _settingsPath =
            Path.Combine(
                directory,
                "settings.json");
    }

    public bool Exists()
    {
        return File.Exists(_settingsPath);
    }

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(_settingsPath))
        {
            return new AppSettings();
        }

        var json =
            await File.ReadAllTextAsync(_settingsPath);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppSettings();
        }

        return JsonSerializer.Deserialize<AppSettings>(
                   json,
                   JsonOptions)
               ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings)
    {
        var json =
            JsonSerializer.Serialize(
                settings,
                JsonOptions);

        await File.WriteAllTextAsync(
            _settingsPath,
            json);
    }

    public string GetSettingsPath()
    {
        return _settingsPath;
    }
}
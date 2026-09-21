using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Claudel.Models;

namespace Claudel.Services;

public class CoverCacheService
{
    private readonly S3Service _s3Service;

    private readonly string _cacheDirectory;

    public CoverCacheService(
        AppSettings settings)
    {
        _s3Service =
            new S3Service(settings);

        var localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

        _cacheDirectory =
            Path.Combine(
                localAppData,
                "Claudel",
                "Cache",
                "Covers");

        Directory.CreateDirectory(
            _cacheDirectory);
    }

    public string GetCachePath(
        Document document)
    {
        return Path.Combine(
            _cacheDirectory,
            $"{document.Id}.jpg");
    }

    public async Task<Bitmap?> GetCoverAsync(
        Document document)
    {
        if (string.IsNullOrWhiteSpace(
                document.CoverS3Key))
        {
            return null;
        }

        var cachePath =
            GetCachePath(document);

        if (!File.Exists(cachePath))
        {
            await _s3Service.DownloadCoverAsync(
                document.CoverS3Key,
                cachePath);
        }

        return await LoadBitmapAsync(
            cachePath);
    }

    public void DeleteCache(
        int documentId)
    {
        var cachePath =
            Path.Combine(
                _cacheDirectory,
                $"{documentId}.jpg");

        if (!File.Exists(cachePath))
        {
            return;
        }

        try
        {
            File.Delete(cachePath);
        }
        catch
        {
        }
    }

    public async Task<Bitmap?> RefreshCacheAsync(
        Document document)
    {
        DeleteCache(document.Id);

        return await GetCoverAsync(
            document);
    }

    private static Task<Bitmap> LoadBitmapAsync(
        string path)
    {
        return Task.Run(
            () =>
            {
                using var stream =
                    File.OpenRead(path);

                return new Bitmap(stream);
            });
    }
}
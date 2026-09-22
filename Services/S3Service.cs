using System;
using System.IO;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Claudel.Models;

namespace Claudel.Services;

public class S3Service
{
    private readonly AppSettings _settings;

    public S3Service(AppSettings settings)
    {
        _settings = settings;
    }

    private AmazonS3Client CreateClient()
    {
        if (string.IsNullOrWhiteSpace(
                _settings.S3.AccessKeyId))
        {
            throw new InvalidOperationException(
                "S3 Access Key ID is not configured.");
        }

        if (string.IsNullOrWhiteSpace(
                _settings.S3.SecretAccessKey))
        {
            throw new InvalidOperationException(
                "S3 Secret Access Key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(
                _settings.S3.Region))
        {
            throw new InvalidOperationException(
                "S3 Region is not configured.");
        }

        if (string.IsNullOrWhiteSpace(
                _settings.S3.Bucket))
        {
            throw new InvalidOperationException(
                "S3 Bucket is not configured.");
        }

        var credentials =
            new BasicAWSCredentials(
                _settings.S3.AccessKeyId,
                _settings.S3.SecretAccessKey);

        var region =
            RegionEndpoint.GetBySystemName(
                _settings.S3.Region);

        return new AmazonS3Client(
            credentials,
            region);
    }

    public async Task UploadCoverAsync(
        string filePath,
        string objectKey)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "Cover image was not found.",
                filePath);
        }

        var extension =
            Path.GetExtension(filePath);

        var contentType =
            extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => throw new InvalidOperationException(
                    "Supported cover formats are JPG, PNG and WebP.")
            };

        using var client =
            CreateClient();

        var request =
            new PutObjectRequest
            {
                BucketName =
                    _settings.S3.Bucket,

                Key =
                    objectKey,

                FilePath =
                    filePath,

                ContentType =
                    contentType
            };

        await client.PutObjectAsync(request);
    }

    public async Task DownloadCoverAsync(
        string objectKey,
        string localPath)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            throw new ArgumentException(
                "S3 object key is empty.",
                nameof(objectKey));
        }

        if (string.IsNullOrWhiteSpace(localPath))
        {
            throw new ArgumentException(
                "Local path is empty.",
                nameof(localPath));
        }

        var directory =
            Path.GetDirectoryName(localPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath =
            localPath + ".tmp";

        try
        {
            using var client =
                CreateClient();

            var request =
                new GetObjectRequest
                {
                    BucketName =
                        _settings.S3.Bucket,

                    Key =
                        objectKey
                };

            using var response =
                await client.GetObjectAsync(request);

            await response.WriteResponseStreamToFileAsync(
                temporaryPath,
                false,
                default);

            File.Move(
                temporaryPath,
                localPath,
                true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                }
            }

            throw;
        }
    }

    public async Task DeleteAsync(
        string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return;
        }

        using var client =
            CreateClient();

        var request =
            new DeleteObjectRequest
            {
                BucketName =
                    _settings.S3.Bucket,

                Key =
                    objectKey
            };

        await client.DeleteObjectAsync(request);
    }
}
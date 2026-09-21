using System;
using System.Collections.Generic;
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

    // S3 Multipart Upload の1パートサイズ
    // 8MB単位でアップロードする
    private const long MultipartPartSize =
        8L * 1024 * 1024;

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

    public async Task UploadPdfAsync(
        string filePath,
        string objectKey,
        IProgress<double>? progress = null)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                "PDF file was not found.",
                filePath);
        }

        if (!filePath.EndsWith(
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Only PDF files are supported.");
        }

        var fileInfo =
            new FileInfo(filePath);

        using var client =
            CreateClient();

        // 小さいファイルは通常のPutObject
        if (fileInfo.Length < MultipartPartSize)
        {
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
                        "application/pdf"
                };

            await client.PutObjectAsync(
                request);

            progress?.Report(100);

            return;
        }

        // 8MB以上はMultipart Upload
        await UploadMultipartAsync(
            client,
            filePath,
            objectKey,
            fileInfo.Length,
            progress);
    }

    private async Task UploadMultipartAsync(
        AmazonS3Client client,
        string filePath,
        string objectKey,
        long totalBytes,
        IProgress<double>? progress)
    {
        var initiateRequest =
            new InitiateMultipartUploadRequest
            {
                BucketName =
                    _settings.S3.Bucket,

                Key =
                    objectKey,

                ContentType =
                    "application/pdf"
            };

        var initiateResponse =
            await client.InitiateMultipartUploadAsync(
                initiateRequest);

        var uploadId =
            initiateResponse.UploadId;

        var completedParts =
            new List<PartETag>();

        long uploadedBytes = 0;

        try
        {
            await using var fileStream =
                new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 1024 * 1024,
                    useAsync: true);

            var partNumber = 1;

            var buffer =
                new byte[MultipartPartSize];

            int bytesRead;

            progress?.Report(0);

            while (
                (bytesRead =
                    await ReadPartAsync(
                        fileStream,
                        buffer)) > 0)
            {
                using var partStream =
                    new MemoryStream(
                        buffer,
                        0,
                        bytesRead,
                        writable: false);

                var uploadPartRequest =
                    new UploadPartRequest
                    {
                        BucketName =
                            _settings.S3.Bucket,

                        Key =
                            objectKey,

                        UploadId =
                            uploadId,

                        PartNumber =
                            partNumber,

                        InputStream =
                            partStream,

                        PartSize =
                            bytesRead,

                        IsLastPart =
                            fileStream.Position >=
                            fileStream.Length
                    };

                var uploadPartResponse =
                    await client.UploadPartAsync(
                        uploadPartRequest);

                completedParts.Add(
                    new PartETag(
                        partNumber,
                        uploadPartResponse.ETag));

                uploadedBytes +=
                    bytesRead;

                var percent =
                    totalBytes > 0
                        ? uploadedBytes * 100.0 /
                          totalBytes
                        : 100.0;

                progress?.Report(
                    Math.Min(
                        percent,
                        100.0));

                partNumber++;
            }

            var completeRequest =
                new CompleteMultipartUploadRequest
                {
                    BucketName =
                        _settings.S3.Bucket,

                    Key =
                        objectKey,

                    UploadId =
                        uploadId
                };

            foreach (var part in completedParts)
            {
                completeRequest.AddPartETags(
                    part);
            }

            await client.CompleteMultipartUploadAsync(
                completeRequest);

            progress?.Report(100);
        }
        catch
        {
            // アップロード途中で失敗した場合、
            // S3側に残っているMultipart Uploadを破棄する
            try
            {
                await client.AbortMultipartUploadAsync(
                    new AbortMultipartUploadRequest
                    {
                        BucketName =
                            _settings.S3.Bucket,

                        Key =
                            objectKey,

                        UploadId =
                            uploadId
                    });
            }
            catch
            {
                // Abort自体の失敗は元の例外を優先
            }

            throw;
        }
    }

    private static async Task<int> ReadPartAsync(
        FileStream stream,
        byte[] buffer)
    {
        var totalRead = 0;

        while (
            totalRead <
            buffer.Length)
        {
            var read =
                await stream.ReadAsync(
                    buffer.AsMemory(
                        totalRead,
                        buffer.Length -
                        totalRead));

            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        return totalRead;
    }

    public async Task<string> DownloadPdfAsync(
        string objectKey)
    {
        if (string.IsNullOrWhiteSpace(
                objectKey))
        {
            throw new ArgumentException(
                "S3 object key is empty.",
                nameof(objectKey));
        }

        var tempDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "Claudel");

        Directory.CreateDirectory(
            tempDirectory);

        var fileName =
            Path.GetFileName(
                objectKey);

        if (string.IsNullOrWhiteSpace(
                fileName))
        {
            fileName =
                $"{Guid.NewGuid():N}.pdf";
        }

        if (!fileName.EndsWith(
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".pdf";
        }

        var localPath =
            Path.Combine(
                tempDirectory,
                fileName);

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
            await client.GetObjectAsync(
                request);

        await response.WriteResponseStreamToFileAsync(
            localPath,
            false,
            default);

        return localPath;
    }

    public async Task DeleteAsync(
        string objectKey)
    {
        if (string.IsNullOrWhiteSpace(
                objectKey))
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

        await client.DeleteObjectAsync(
            request);
    }
}

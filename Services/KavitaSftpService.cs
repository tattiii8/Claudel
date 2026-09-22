using System;
using System.IO;
using System.Threading.Tasks;
using Claudel.Models;
using Renci.SshNet;

namespace Claudel.Services;

public class KavitaSftpService
{
    private readonly KavitaSftpSettings _settings;

    public KavitaSftpService(
        KavitaSftpSettings settings)
    {
        _settings =
            settings;
    }

    public async Task UploadPdfAsync(
        string localFilePath,
        string documentTitle,
        IProgress<ulong>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(
                localFilePath))
        {
            throw new ArgumentException(
                "PDFファイルが指定されていません。",
                nameof(localFilePath));
        }

        if (!File.Exists(localFilePath))
        {
            throw new FileNotFoundException(
                "PDFファイルが見つかりません。",
                localFilePath);
        }

        if (!string.Equals(
                Path.GetExtension(localFilePath),
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "PDFファイルのみアップロードできます。");
        }

        if (string.IsNullOrWhiteSpace(
                documentTitle))
        {
            throw new ArgumentException(
                "Documentタイトルが指定されていません。",
                nameof(documentTitle));
        }

        ValidateSettings();

        var remoteDirectory =
            _settings.RemotePath.TrimEnd('/');

        /*
         * Kavitaでは、
         *
         * documents/
         *   Documentタイトル/
         *     PDFファイル.pdf
         *
         * の構造にする。
         */
        var seriesDirectoryName =
            SanitizeDirectoryName(
                documentTitle);

        if (string.IsNullOrWhiteSpace(
                seriesDirectoryName))
        {
            throw new InvalidOperationException(
                "DocumentタイトルからKavitaのディレクトリ名を作成できません。");
        }

        var seriesDirectory =
            $"{remoteDirectory}/{seriesDirectoryName}";

        var fileName =
            Path.GetFileName(localFilePath);

        var remotePath =
            $"{seriesDirectory}/{fileName}";

        await Task.Run(() =>
        {
            using var keyFile =
                new PrivateKeyFile(
                    _settings.PrivateKeyPath);

            var authentication =
                new PrivateKeyAuthenticationMethod(
                    _settings.User,
                    keyFile);

            var connectionInfo =
                new ConnectionInfo(
                    _settings.Host,
                    _settings.Port,
                    _settings.User,
                    authentication);

            using var client =
                new SftpClient(connectionInfo);

            client.Connect();

            if (!client.Exists(
                    remoteDirectory))
            {
                throw new DirectoryNotFoundException(
                    "KavitaのPDFディレクトリが存在しません。\n\n" +
                    remoteDirectory);
            }

            /*
             * Documentタイトルのフォルダが
             * 存在しなければ作成する。
             */
            if (!client.Exists(
                    seriesDirectory))
            {
                client.CreateDirectory(
                    seriesDirectory);
            }

            using var stream =
                File.OpenRead(localFilePath);

            client.UploadFile(
                stream,
                remotePath,
                uploadedBytes =>
                {
                    progress?.Report(
                        uploadedBytes);
                });

            client.Disconnect();
        });
    }

    private static string SanitizeDirectoryName(
        string name)
    {
        var result =
            name.Trim();

        /*
         * Linuxのパスとして使えない文字を
         * '_' に置換する。
         */
        foreach (var character in
                 Path.GetInvalidFileNameChars())
        {
            result =
                result.Replace(
                    character,
                    '_');
        }

        /*
         * Linuxでは '\' は通常問題ないが、
         * パス区切りとして扱われる可能性を
         * 避けるため置換する。
         */
        result =
            result
                .Replace(
                    "/",
                    "_")
                .Replace(
                    "\\",
                    "_");

        return result.Trim();
    }

    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(
                _settings.Host))
        {
            throw new InvalidOperationException(
                "Kavita SFTP Hostが設定されていません。");
        }

        if (_settings.Port <= 0 ||
            _settings.Port > 65535)
        {
            throw new InvalidOperationException(
                "Kavita SFTP Portが不正です。");
        }

        if (string.IsNullOrWhiteSpace(
                _settings.User))
        {
            throw new InvalidOperationException(
                "Kavita SFTP Userが設定されていません。");
        }

        if (string.IsNullOrWhiteSpace(
                _settings.PrivateKeyPath))
        {
            throw new InvalidOperationException(
                "Kavita SFTP Private Key Pathが設定されていません。");
        }

        if (!File.Exists(
                _settings.PrivateKeyPath))
        {
            throw new FileNotFoundException(
                "SFTP Private Keyが見つかりません。",
                _settings.PrivateKeyPath);
        }

        if (string.IsNullOrWhiteSpace(
                _settings.RemotePath))
        {
            throw new InvalidOperationException(
                "Kavita SFTP Remote Pathが設定されていません。");
        }
    }
}
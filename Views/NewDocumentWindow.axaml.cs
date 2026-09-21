using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Claudel.Models;
using Claudel.Repositories;
using Claudel.Services;

namespace Claudel;

public partial class NewDocumentWindow : Window
{
    private readonly DocumentRepository _repository;
    private readonly S3Service _s3Service;

    private string? _pdfPath;

    public NewDocumentWindow(
        DocumentRepository repository,
        AppSettings settings)
    {
        InitializeComponent();

        _repository =
            repository;

        _s3Service =
            new S3Service(settings);

        UploadProgressPanel.IsVisible =
            false;
    }

    private async void BrowsePdf_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (!SaveButton.IsEnabled)
        {
            return;
        }

        var files =
            await StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title =
                        "Select PDF",

                    AllowMultiple =
                        false,

                    FileTypeFilter =
                    [
                        new FilePickerFileType("PDF")
                        {
                            Patterns =
                            [
                                "*.pdf"
                            ]
                        }
                    ]
                });

        if (files.Count == 0)
        {
            return;
        }

        var file =
            files[0];

        _pdfPath =
            file.Path.LocalPath;

        PdfPathTextBox.Text =
            _pdfPath;
    }

    private async void Save_Click(
        object? sender,
        RoutedEventArgs e)
    {
        try
        {
            var title =
                TitleTextBox.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(title))
            {
                await ShowErrorAsync(
                    "Title is required.");

                return;
            }

            int? year = null;

            var yearText =
                YearTextBox.Text?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(yearText))
            {
                if (!int.TryParse(
                        yearText,
                        out var parsedYear))
                {
                    await ShowErrorAsync(
                        "Year must be a number.");

                    return;
                }

                year =
                    parsedYear;
            }

            // PDFは任意。
            // 選択されている場合だけ存在確認・拡張子確認を行う。
            if (!string.IsNullOrWhiteSpace(_pdfPath))
            {
                if (!File.Exists(_pdfPath))
                {
                    await ShowErrorAsync(
                        "PDF file was not found.");

                    return;
                }

                if (!string.Equals(
                        Path.GetExtension(_pdfPath),
                        ".pdf",
                        StringComparison.OrdinalIgnoreCase))
                {
                    await ShowErrorAsync(
                        "Only PDF files are supported.");

                    return;
                }
            }

            SaveButton.IsEnabled =
                false;

            CancelButton.IsEnabled =
                false;

            BrowsePdfButton.IsEnabled =
                false;

            TitleTextBox.IsEnabled =
                false;

            AuthorTextBox.IsEnabled =
                false;

            CategoryTextBox.IsEnabled =
                false;

            TagsTextBox.IsEnabled =
                false;

            YearTextBox.IsEnabled =
                false;

            var document =
                new Document
                {
                    Title =
                        title,

                    Author =
                        AuthorTextBox.Text?.Trim() ?? "",

                    Category =
                        CategoryTextBox.Text?.Trim() ?? "",

                    Tags =
                        TagsTextBox.Text?.Trim() ?? "",

                    Year =
                        year,

                    S3Key =
                        ""
                };

            // PDFが選択されている場合だけS3へアップロード
            if (!string.IsNullOrWhiteSpace(_pdfPath))
            {
                UploadProgressPanel.IsVisible =
                    true;

                UploadProgressBar.Value =
                    0;

                UploadProgressText.Text =
                    "0%";

                UploadStatusText.Text =
                    "Uploading PDF...";

                var objectKey =
                    $"pdf/{Guid.NewGuid():N}.pdf";

                var progress =
                    new Progress<double>(
                        percent =>
                        {
                            UploadProgressBar.Value =
                                Math.Clamp(
                                    percent,
                                    0,
                                    100);

                            UploadProgressText.Text =
                                $"{percent:0}%";

                            UploadStatusText.Text =
                                $"Uploading PDF... {percent:0}%";
                        });

                await _s3Service.UploadPdfAsync(
                    _pdfPath,
                    objectKey,
                    progress);

                document.S3Key =
                    objectKey;

                UploadProgressBar.Value =
                    100;

                UploadProgressText.Text =
                    "100%";

                UploadStatusText.Text =
                    "Saving document...";
            }

            await _repository.CreateAsync(
                document);

            Close();
        }
        catch (Exception ex)
        {
            SaveButton.IsEnabled =
                true;

            CancelButton.IsEnabled =
                true;

            BrowsePdfButton.IsEnabled =
                true;

            TitleTextBox.IsEnabled =
                true;

            AuthorTextBox.IsEnabled =
                true;

            CategoryTextBox.IsEnabled =
                true;

            TagsTextBox.IsEnabled =
                true;

            YearTextBox.IsEnabled =
                true;

            await ShowErrorAsync(
                $"文書の登録に失敗しました。\n\n{ex.Message}");
        }
    }

    private void Cancel_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (!SaveButton.IsEnabled)
        {
            return;
        }

        Close();
    }

    private async Task ShowErrorAsync(
        string message)
    {
        var dialog =
            new Window
            {
                Title =
                    "Error",

                Width =
                    500,

                Height =
                    200,

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

        await dialog.ShowDialog(this);
    }
}

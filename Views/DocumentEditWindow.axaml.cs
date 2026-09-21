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

public partial class DocumentEditWindow : Window
{
    private readonly Document _document;

    private readonly DocumentRepository _repository;

    private readonly S3Service _s3Service;

    private string? _pdfPath;

    public DocumentEditWindow(
        Document document,
        DocumentRepository repository,
        AppSettings settings)
    {
        InitializeComponent();

        _document =
            document;

        _repository =
            repository;

        _s3Service =
            new S3Service(settings);

        TitleTextBox.Text =
            document.Title;

        AuthorTextBox.Text =
            document.Author;

        CategoryTextBox.Text =
            document.Category;

        TagsTextBox.Text =
            document.Tags;

        YearTextBox.Text =
            document.Year?.ToString() ?? "";

        PdfPathTextBox.Text =
            string.IsNullOrWhiteSpace(
                document.S3Key)
                ? ""
                : "Existing PDF";

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

            if (!string.IsNullOrWhiteSpace(
                    _pdfPath))
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

            SetSavingState(true);

            _document.Title =
                title;

            _document.Author =
                AuthorTextBox.Text?.Trim() ?? "";

            _document.Category =
                CategoryTextBox.Text?.Trim() ?? "";

            _document.Tags =
                TagsTextBox.Text?.Trim() ?? "";

            _document.Year =
                year;

            // PDFが選択されている場合だけS3へアップロード
            if (!string.IsNullOrWhiteSpace(
                    _pdfPath))
            {
                SetUploadStatus(
                    "Uploading PDF...",
                    0);

                var objectKey =
                    $"pdf/{Guid.NewGuid():N}.pdf";

                var progress =
                    new Progress<double>(
                        percent =>
                        {
                            SetUploadStatus(
                                $"Uploading PDF... {percent:0}%",
                                percent);
                        });

                await _s3Service.UploadPdfAsync(
                    _pdfPath,
                    objectKey,
                    progress);

                _document.S3Key =
                    objectKey;

                SetUploadStatus(
                    "Saving document...",
                    100);
            }

            var updated =
                await _repository.UpdateAsync(
                    _document);

            if (!updated)
            {
                throw new InvalidOperationException(
                    "文書を更新できませんでした。");
            }

            Close();
        }
        catch (Exception ex)
        {
            SetSavingState(false);

            await ShowErrorAsync(
                ex.Message);
        }
    }

    private void SetSavingState(
        bool saving)
    {
        SaveButton.IsEnabled =
            !saving;

        CancelButton.IsEnabled =
            !saving;

        BrowsePdfButton.IsEnabled =
            !saving;

        TitleTextBox.IsEnabled =
            !saving;

        AuthorTextBox.IsEnabled =
            !saving;

        CategoryTextBox.IsEnabled =
            !saving;

        TagsTextBox.IsEnabled =
            !saving;

        YearTextBox.IsEnabled =
            !saving;

        if (saving)
        {
            UploadProgressPanel.IsVisible =
                true;

            UploadProgressBar.Value =
                0;

            UploadProgressText.Text =
                "0%";

            UploadStatusText.Text =
                "Preparing...";
        }
    }

    private void SetUploadStatus(
        string status,
        double percent)
    {
        UploadProgressPanel.IsVisible =
            true;

        UploadProgressBar.Value =
            Math.Clamp(
                percent,
                0,
                100);

        UploadProgressText.Text =
            $"{percent:0}%";

        UploadStatusText.Text =
            status;
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

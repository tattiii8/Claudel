using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
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
private readonly OpenLibraryService _openLibraryService;

private readonly ObservableCollection<BookCoverCandidate>
    _coverCandidates = new();

private string? _pdfPath;

private BookCoverCandidate? _selectedCover;

private string? _selectedCoverPath;

private string? _newCoverS3Key;

public DocumentEditWindow(
    Document document,
    DocumentRepository repository,
    AppSettings settings)
{
    InitializeComponent();

    _document = document;
    _repository = repository;

    _s3Service =
        new S3Service(settings);

    _openLibraryService =
        new OpenLibraryService();

    CoverCandidatesListBox.ItemsSource =
        _coverCandidates;

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
        string.IsNullOrWhiteSpace(document.S3Key)
            ? ""
            : "Existing PDF";

    UploadProgressPanel.IsVisible =
        false;

    _ = LoadExistingCoverAsync();
}

private async Task LoadExistingCoverAsync()
{
    if (string.IsNullOrWhiteSpace(
            _document.CoverS3Key))
    {
        return;
    }

    try
    {
        var tempPath =
            Path.Combine(
                Path.GetTempPath(),
                $"claudel-edit-cover-{Guid.NewGuid():N}");

        await _s3Service.DownloadCoverAsync(
            _document.CoverS3Key,
            tempPath);

        await using var stream =
            File.OpenRead(tempPath);

        CoverImage.Source =
            new Bitmap(stream);

        try
        {
            File.Delete(tempPath);
        }
        catch
        {
        }
    }
    catch
    {
        CoverImage.Source = null;
    }
}

private async void SearchCover_Click(
    object? sender,
    RoutedEventArgs e)
{
    await SearchCoversAsync();
}

private async Task SearchCoversAsync()
{
    var title =
        TitleTextBox.Text?.Trim() ?? "";

    var author =
        AuthorTextBox.Text?.Trim() ?? "";

    if (string.IsNullOrWhiteSpace(title) &&
        string.IsNullOrWhiteSpace(author))
    {
        await ShowErrorAsync(
            "Please enter a title or author.");

        return;
    }

    try
    {
        SearchCoverButton.IsEnabled = false;
        SearchCoverButton.Content = "Searching...";

        _coverCandidates.Clear();

        _selectedCover = null;
        _selectedCoverPath = null;

        CoverCandidatesListBox.SelectedItem = null;

        var results =
            await _openLibraryService.SearchAsync(
                title,
                author);

        foreach (var result in results)
        {
            _coverCandidates.Add(result);
        }

        if (_coverCandidates.Count == 0)
        {
            await ShowErrorAsync(
                "No cover candidates were found.");
        }
    }
    catch (Exception ex)
    {
        await ShowErrorAsync(
            $"Cover search failed.\n\n{ex.Message}");
    }
    finally
    {
        SearchCoverButton.IsEnabled = true;
        SearchCoverButton.Content = "Search Web";
    }
}

private async void CoverCandidatesListBox_SelectionChanged(
    object? sender,
    SelectionChangedEventArgs e)
{
    if (CoverCandidatesListBox.SelectedItem
        is not BookCoverCandidate candidate)
    {
        return;
    }

    _selectedCover =
        candidate;

    _selectedCoverPath =
        null;

    if (string.IsNullOrWhiteSpace(
            candidate.CoverUrl))
    {
        return;
    }

    try
    {
        using var httpClient =
            new HttpClient();

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Claudel/1.0 (book management application)");

        var bytes =
            await httpClient.GetByteArrayAsync(
                candidate.CoverUrl);

        await using var stream =
            new MemoryStream(bytes);

        CoverImage.Source =
            new Bitmap(stream);
    }
    catch
    {
        CoverImage.Source = null;
    }
}

private async void UploadCover_Click(
    object? sender,
    RoutedEventArgs e)
{
    var files =
        await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title =
                    "Select Cover Image",

                AllowMultiple =
                    false,

                FileTypeFilter =
                [
                    new FilePickerFileType("Image")
                    {
                        Patterns =
                        [
                            "*.jpg",
                            "*.jpeg",
                            "*.png",
                            "*.webp"
                        ]
                    }
                ]
            });

    if (files.Count == 0)
    {
        return;
    }

    var path =
        files[0].Path.LocalPath;

    if (string.IsNullOrWhiteSpace(path) ||
        !File.Exists(path))
    {
        await ShowErrorAsync(
            "The selected image could not be accessed.");

        return;
    }

    try
    {
        var extension =
            Path.GetExtension(path);

        if (!IsSupportedImageExtension(extension))
        {
            await ShowErrorAsync(
                "Please select a JPG, JPEG, PNG, or WebP image.");

            return;
        }

        await using var stream =
            File.OpenRead(path);

        CoverImage.Source =
            new Bitmap(stream);

        _selectedCover =
            null;

        CoverCandidatesListBox.SelectedItem =
            null;

        _selectedCoverPath =
            path;
    }
    catch (Exception ex)
    {
        await ShowErrorAsync(
            $"Failed to load the image.\n\n{ex.Message}");
    }
}

private static bool IsSupportedImageExtension(
    string? extension)
{
    return string.Equals(
               extension,
               ".jpg",
               StringComparison.OrdinalIgnoreCase)
           ||
           string.Equals(
               extension,
               ".jpeg",
               StringComparison.OrdinalIgnoreCase)
           ||
           string.Equals(
               extension,
               ".png",
               StringComparison.OrdinalIgnoreCase)
           ||
           string.Equals(
               extension,
               ".webp",
               StringComparison.OrdinalIgnoreCase);
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

        /*
         * Cover:
         *
         * 1. Local image
         * 2. Selected Open Library image
         * 3. Existing cover
         */

        if (!string.IsNullOrWhiteSpace(
                _selectedCoverPath))
        {
            SetUploadStatus(
                "Uploading cover...",
                0);

            _newCoverS3Key =
                await UploadLocalCoverAsync(
                    _selectedCoverPath);

            _document.CoverS3Key =
                _newCoverS3Key;

            SetUploadStatus(
                "Cover uploaded.",
                100);
        }
        else if (_selectedCover != null &&
                 !string.IsNullOrWhiteSpace(
                     _selectedCover.CoverUrl))
        {
            SetUploadStatus(
                "Downloading cover...",
                0);

            _newCoverS3Key =
                await UploadWebCoverAsync(
                    _selectedCover);

            if (!string.IsNullOrWhiteSpace(
                    _newCoverS3Key))
            {
                _document.CoverS3Key =
                    _newCoverS3Key;
            }

            SetUploadStatus(
                "Cover uploaded.",
                100);
        }

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

        var oldCoverS3Key =
            _document.CoverS3Key;

        var updated =
            await _repository.UpdateAsync(
                _document);

        if (!updated)
        {
            throw new InvalidOperationException(
                "文書を更新できませんでした。");
        }

        /*
         * DB更新成功後に古いカバーを削除。
         */
        if (!string.IsNullOrWhiteSpace(
                _newCoverS3Key) &&
            !string.IsNullOrWhiteSpace(
                oldCoverS3Key) &&
            !string.Equals(
                oldCoverS3Key,
                _newCoverS3Key,
                StringComparison.Ordinal))
        {
            try
            {
                await _s3Service.DeleteAsync(
                    oldCoverS3Key);
            }
            catch
            {
                // DB更新を優先する。
                // 古いS3オブジェクトは残っても
                // 次回以降の整理対象とする。
            }
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

private async Task<string> UploadLocalCoverAsync(
    string filePath)
{
    var extension =
        Path.GetExtension(filePath)
            .ToLowerInvariant();

    var s3Extension =
        extension switch
        {
            ".jpeg" => ".jpg",
            _ => extension
        };

    var s3Key =
        $"covers/{Guid.NewGuid():N}{s3Extension}";

    await _s3Service.UploadCoverAsync(
        filePath,
        s3Key);

    return s3Key;
}

private async Task<string?> UploadWebCoverAsync(
    BookCoverCandidate candidate)
{
    var tempCoverPath =
        await _openLibraryService
            .DownloadCoverAsync(candidate);

    if (string.IsNullOrWhiteSpace(
            tempCoverPath))
    {
        return null;
    }

    try
    {
        var coverS3Key =
            $"covers/{Guid.NewGuid():N}.jpg";

        await _s3Service.UploadCoverAsync(
            tempCoverPath,
            coverS3Key);

        return coverS3Key;
    }
    finally
    {
        try
        {
            File.Delete(tempCoverPath);
        }
        catch
        {
        }
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

    SearchCoverButton.IsEnabled =
        !saving;

    UploadCoverButton.IsEnabled =
        !saving;

    CoverCandidatesListBox.IsEnabled =
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

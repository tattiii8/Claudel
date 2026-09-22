using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
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

    private BookCoverCandidate? _selectedCover;

    private string? _selectedCoverPath;

    private string? _newCoverS3Key;

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

        _openLibraryService =
            new OpenLibraryService();

        CoverCandidatesListBox.ItemsSource =
            _coverCandidates;

        TitleTextBox.Text =
            document.Title;

        AuthorTextBox.Text =
            string.Join(
                Environment.NewLine,
                document.Authors
                    .OrderBy(x => x.Order)
                    .Select(x => x.Name));

        CategoryTextBox.Text =
            document.Category;

        TagsTextBox.Text =
            string.Join(
                ", ",
                document.Tags
                    .Select(x => x.Name));

        PublicationDateTextBox.Text =
            document.PublicationDate?
                .ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture)
            ?? "";

        UploadProgressPanel.IsVisible =
            false;

        _ = LoadExistingCoverAsync();
    }

    /*
     * Load existing cover from S3.
     */
    private async Task LoadExistingCoverAsync()
    {
        if (string.IsNullOrWhiteSpace(
                _document.CoverS3Key))
        {
            return;
        }

        string? tempPath = null;

        try
        {
            tempPath =
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
        }
        catch
        {
            CoverImage.Source =
                null;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                }
            }
        }
    }

    /*
     * Web cover search.
     */
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
            SearchCoverButton.IsEnabled =
                false;

            SearchCoverButton.Content =
                "Searching...";

            _coverCandidates.Clear();

            _selectedCover =
                null;

            _selectedCoverPath =
                null;

            CoverCandidatesListBox.SelectedItem =
                null;

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
            SearchCoverButton.IsEnabled =
                true;

            SearchCoverButton.Content =
                "Search Web";
        }
    }

    /*
     * Select web cover.
     */
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
            CoverImage.Source =
                null;
        }
    }

    /*
     * Select local cover image.
     */
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

            if (!IsSupportedImageExtension(
                    extension))
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

    /*
     * Save document.
     */
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

            DateTime? publicationDate =
                null;

            var publicationDateText =
                PublicationDateTextBox.Text?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(
                    publicationDateText))
            {
                if (!DateTime.TryParseExact(
                        publicationDateText,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsedDate))
                {
                    await ShowErrorAsync(
                        "Publication Date must be in yyyy-MM-dd format.");

                    return;
                }

                publicationDate =
                    parsedDate.Date;
            }

            SetSavingState(true);

            /*
             * Authors
             */
            var authors =
                new List<Author>();

            var authorLines =
                (AuthorTextBox.Text ?? "")
                    .Split(
                        new[]
                        {
                            "\r\n",
                            "\n",
                            "\r"
                        },
                        StringSplitOptions.RemoveEmptyEntries);

            var authorOrder =
                1;

            foreach (var authorName in authorLines)
            {
                var name =
                    authorName.Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                authors.Add(
                    new Author
                    {
                        Name =
                            name,

                        Order =
                            authorOrder++
                    });
            }

            /*
             * Tags
             */
            var tags =
                new List<Tag>();

            var tagNames =
                (TagsTextBox.Text ?? "")
                    .Split(
                        new[]
                        {
                            ',',
                            '、',
                            '\r',
                            '\n'
                        },
                        StringSplitOptions.RemoveEmptyEntries);

            foreach (var tagName in tagNames)
            {
                var name =
                    tagName.Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (tags.Any(
                        x => string.Equals(
                            x.Name,
                            name,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                tags.Add(
                    new Tag
                    {
                        Name =
                            name
                    });
            }

            /*
             * Update metadata.
             */
            _document.Title =
                title;

            _document.Authors =
                authors;

            _document.Category =
                CategoryTextBox.Text?.Trim() ?? "";

            _document.Tags =
                tags;

            _document.PublicationDate =
                publicationDate;

            /*
             * Cover
             *
             * Priority:
             * 1. Local image
             * 2. Selected Open Library image
             * 3. Existing cover
             */
            var oldCoverS3Key =
                _document.CoverS3Key;

            _newCoverS3Key =
                null;

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

                    SetUploadStatus(
                        "Cover uploaded.",
                        100);
                }
            }

            /*
             * Save metadata.
             */
            SetUploadStatus(
                "Saving document...",
                100);

            var updated =
                await _repository.UpdateAsync(
                    _document);

            if (!updated)
            {
                throw new InvalidOperationException(
                    "文書を更新できませんでした。");
            }

            /*
             * Delete old cover only after
             * the database update succeeded.
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
                    /*
                     * Database update has already succeeded,
                     * so do not fail the edit operation because
                     * cleanup of the old cover failed.
                     */
                }
            }

            Close(true);
        }
        catch (Exception ex)
        {
            SetSavingState(false);

            await ShowErrorAsync(
                $"Failed to save document.\n\n{ex.Message}");
        }
    }

    /*
     * Upload local cover to S3.
     */
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

    /*
     * Download Open Library cover,
     * then upload it to S3.
     */
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

    /*
     * Enable / disable controls.
     */
    private void SetSavingState(
        bool saving)
    {
        SaveButton.IsEnabled =
            !saving;

        CancelButton.IsEnabled =
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

        PublicationDateTextBox.IsEnabled =
            !saving;
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

    /*
     * Cancel.
     */
    private void Cancel_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (!SaveButton.IsEnabled)
        {
            return;
        }

        Close(false);
    }

    /*
     * Error dialog.
     */
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
                    220,

                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner
            };

        var button =
            new Button
            {
                Content =
                    "OK",

                Width =
                    80,

                HorizontalAlignment =
                    Avalonia.Layout.HorizontalAlignment.Right
            };

        button.Click += (_, _) =>
        {
            dialog.Close();
        };

        dialog.Content =
            new StackPanel
            {
                Margin =
                    new Thickness(20),

                Spacing =
                    12,

                Children =
                {
                    new TextBlock
                    {
                        Text =
                            message,

                        TextWrapping =
                            Avalonia.Media.TextWrapping.Wrap
                    },

                    button
                }
            };

        await dialog.ShowDialog(this);
    }
}
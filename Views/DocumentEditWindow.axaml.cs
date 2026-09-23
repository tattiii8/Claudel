using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
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

    private readonly CrossrefService _crossrefService;

    private readonly ObservableCollection<CrossrefWork>
        _crossrefResults = new();

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

        _crossrefService =
            new CrossrefService();

        CrossrefResultsListBox.ItemsSource =
            _crossrefResults;

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

        JournalNameTextBox.Text =
            document.JournalName;

        VolumeTextBox.Text =
            document.Volume;

        IssueTextBox.Text =
            document.Issue;

        PagesTextBox.Text =
            document.Pages;

        DoiTextBox.Text =
            document.DOI;

        var documentType =
            string.IsNullOrWhiteSpace(document.DocumentType)
                ? "Book"
                : document.DocumentType;

        SetDocumentType(documentType);

        CrossrefTitleTextBox.Text =
            document.Title;

        CrossrefAuthorTextBox.Text =
            document.Authors
                .OrderBy(x => x.Order)
                .Select(x => x.Name)
                .FirstOrDefault()
            ?? "";

        UploadProgressPanel.IsVisible =
            false;

        _ = LoadExistingCoverAsync();
    }

    /*
     * Set document type.
     */
    private void SetDocumentType(
        string documentType)
    {
        var isJournal =
            string.Equals(
                documentType,
                "Journal",
                StringComparison.OrdinalIgnoreCase);

        DocumentTypeComboBox.SelectedIndex =
            isJournal ? 1 : 0;

        JournalSearchPanel.IsVisible =
            isJournal;

        JournalMetadataPanel.IsVisible =
            isJournal;
    }

    /*
     * Change document type.
     */
    private void DocumentTypeComboBox_SelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (DocumentTypeComboBox.SelectedItem
            is not ComboBoxItem item)
        {
            return;
        }

        var documentType =
            item.Tag?.ToString()
            ?? "Book";

        var isJournal =
            string.Equals(
                documentType,
                "Journal",
                StringComparison.OrdinalIgnoreCase);

        JournalSearchPanel.IsVisible =
            isJournal;

        JournalMetadataPanel.IsVisible =
            isJournal;

        if (isJournal)
        {
            CrossrefTitleTextBox.Text =
                TitleTextBox.Text ?? "";

            CrossrefAuthorTextBox.Text =
                GetFirstAuthor();
        }
    }

    private string GetFirstAuthor()
    {
        var firstLine =
            (AuthorTextBox.Text ?? "")
                .Split(
                    new[]
                    {
                        "\r\n",
                        "\n",
                        "\r"
                    },
                    StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

        return firstLine?.Trim() ?? "";
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
     * Crossref search.
     */
    private async void CrossrefSearch_Click(
        object? sender,
        RoutedEventArgs e)
    {
        await SearchCrossrefAsync();
    }

    private async Task SearchCrossrefAsync()
    {
        var title =
            CrossrefTitleTextBox.Text?.Trim() ?? "";

        var author =
            CrossrefAuthorTextBox.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(title) &&
            string.IsNullOrWhiteSpace(author))
        {
            await ShowErrorAsync(
                "Please enter a title or author.");

            return;
        }

        try
        {
            CrossrefSearchButton.IsEnabled =
                false;

            CrossrefSearchButton.Content =
                "Searching...";

            CrossrefStatusText.IsVisible =
                true;

            CrossrefStatusText.Text =
                "Searching Crossref...";

            _crossrefResults.Clear();

            var results =
                await _crossrefService
                    .SearchJournalArticlesAsync(
                        title,
                        author);

            foreach (var result in results)
            {
                _crossrefResults.Add(result);
            }

            if (_crossrefResults.Count == 0)
            {
                CrossrefStatusText.Text =
                    "No results found.";

                return;
            }

            CrossrefStatusText.Text =
                $"{_crossrefResults.Count} result(s) found.";
        }
        catch (Exception ex)
        {
            CrossrefStatusText.Text =
                "Search failed.";

            await ShowErrorAsync(
                $"Crossref search failed.\n\n{ex.Message}");
        }
        finally
        {
            CrossrefSearchButton.IsEnabled =
                true;

            CrossrefSearchButton.Content =
                "Crossref Search";
        }
    }

    /*
     * Select Crossref result.
     */
    private void CrossrefResultsListBox_SelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (CrossrefResultsListBox.SelectedItem
            is not CrossrefWork work)
        {
            return;
        }

        ApplyCrossrefWork(work);
    }

    private void ApplyCrossrefWork(
        CrossrefWork work)
    {
        TitleTextBox.Text =
            work.Title;

        AuthorTextBox.Text =
            string.Join(
                Environment.NewLine,
                work.Authors);

        JournalNameTextBox.Text =
            work.JournalName;

        VolumeTextBox.Text =
            work.Volume;

        IssueTextBox.Text =
            work.Issue;

        PagesTextBox.Text =
            work.Pages;

        DoiTextBox.Text =
            work.DOI;

        PublicationDateTextBox.Text =
            work.PublicationDate?
                .ToString(
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture)
            ?? "";

        CrossrefTitleTextBox.Text =
            work.Title;

        CrossrefAuthorTextBox.Text =
            work.Authors.FirstOrDefault()
            ?? "";
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

            var documentType =
                GetSelectedDocumentType();

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

            _document.DocumentType =
                documentType;

            _document.Authors =
                authors;

            _document.Category =
                CategoryTextBox.Text?.Trim() ?? "";

            _document.Tags =
                tags;

            _document.PublicationDate =
                publicationDate;

            if (string.Equals(
                    documentType,
                    "Journal",
                    StringComparison.OrdinalIgnoreCase))
            {
                _document.JournalName =
                    JournalNameTextBox.Text?.Trim() ?? "";

                _document.Volume =
                    VolumeTextBox.Text?.Trim() ?? "";

                _document.Issue =
                    IssueTextBox.Text?.Trim() ?? "";

                _document.Pages =
                    PagesTextBox.Text?.Trim() ?? "";

                _document.DOI =
                    DoiTextBox.Text?.Trim() ?? "";
            }
            else
            {
                _document.JournalName =
                    "";

                _document.Volume =
                    "";

                _document.Issue =
                    "";

                _document.Pages =
                    "";

                _document.DOI =
                    "";
            }

            /*
             * Cover
             *
             * Only local image upload is supported.
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
                    "Failed to update the document.");
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
                     * Database update has already succeeded.
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

    private string GetSelectedDocumentType()
    {
        if (DocumentTypeComboBox.SelectedItem
            is ComboBoxItem item)
        {
            return item.Tag?.ToString()
                   ?? "Book";
        }

        return "Book";
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
     * Enable / disable controls.
     */
    private void SetSavingState(
        bool saving)
    {
        SaveButton.IsEnabled =
            !saving;

        CancelButton.IsEnabled =
            !saving;

        UploadCoverButton.IsEnabled =
            !saving;

        CrossrefSearchButton.IsEnabled =
            !saving;

        CrossrefResultsListBox.IsEnabled =
            !saving;

        DocumentTypeComboBox.IsEnabled =
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

        JournalNameTextBox.IsEnabled =
            !saving;

        VolumeTextBox.IsEnabled =
            !saving;

        IssueTextBox.IsEnabled =
            !saving;

        PagesTextBox.IsEnabled =
            !saving;

        DoiTextBox.IsEnabled =
            !saving;

        CrossrefTitleTextBox.IsEnabled =
            !saving;

        CrossrefAuthorTextBox.IsEnabled =
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
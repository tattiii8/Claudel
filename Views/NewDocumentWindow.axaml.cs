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
using Claudel.Models;
using Claudel.Repositories;
using Claudel.Services;

namespace Claudel;

public partial class NewDocumentWindow : Window
{
    private readonly DocumentRepository _repository;

    private readonly CrossrefService _crossrefService;

    private readonly S3Service _s3Service;

    private readonly ObservableCollection<CrossrefWork>
        _crossrefResults = new();

    private string? _selectedCoverPath;

    public NewDocumentWindow(
        DocumentRepository repository,
        AppSettings settings)
    {
        InitializeComponent();

        _repository = repository;

        _crossrefService =
            new CrossrefService();

        _s3Service =
            new S3Service(settings);

        CrossrefResultsListBox.ItemsSource =
            _crossrefResults;

        // Initialize after InitializeComponent().
        // Do not use SelectedIndex="0" in AXAML.
        DocumentTypeComboBox.SelectedIndex = 0;

        SetDocumentType(
            DocumentTypes.Book);
    }

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
            ?? DocumentTypes.Book;

        SetDocumentType(documentType);
    }

    private void SetDocumentType(
        string documentType)
    {
        var isJournal =
            string.Equals(
                documentType,
                DocumentTypes.Journal,
                StringComparison.Ordinal);

        JournalSearchPanel.IsVisible =
            isJournal;

        if (!isJournal)
        {
            _crossrefResults.Clear();

            CrossrefResultsListBox.SelectedItem =
                null;
        }
    }

    private async void CrossrefSearch_Click(
        object? sender,
        RoutedEventArgs e)
    {
        await SearchCrossrefAsync();
    }

    private async Task SearchCrossrefAsync()
    {
        var title =
            CrossrefTitleTextBox.Text?.Trim()
            ?? "";

        var author =
            CrossrefAuthorTextBox.Text?.Trim()
            ?? "";

        if (string.IsNullOrWhiteSpace(title) &&
            string.IsNullOrWhiteSpace(author))
        {
            await ShowMessageAsync(
                "Please enter an article title or author.");

            return;
        }

        try
        {
            CrossrefSearchButton.IsEnabled =
                false;

            CrossrefSearchButton.Content =
                "Searching...";

            _crossrefResults.Clear();

            CrossrefResultsListBox.SelectedItem =
                null;

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
                await ShowMessageAsync(
                    "No journal articles were found.");
            }
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(
                $"Crossref search failed.\n\n{ex.Message}");
        }
        finally
        {
            CrossrefSearchButton.IsEnabled =
                true;

            CrossrefSearchButton.Content =
                "Search Crossref";
        }
    }

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

        if (work.PublicationDate.HasValue)
        {
            PublicationDateTextBox.Text =
                work.PublicationDate.Value
                    .ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture);
        }
    }

    private async void UploadCover_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var files =
            await StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Select Cover Image",
                    AllowMultiple = false
                });

        if (files.Count == 0)
        {
            return;
        }

        var file = files[0];

        var path =
            file.Path.LocalPath;

        if (string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path))
        {
            await ShowMessageAsync(
                "The selected image could not be accessed.");

            return;
        }

        var extension =
            Path.GetExtension(path);

        if (!IsSupportedImageExtension(extension))
        {
            await ShowMessageAsync(
                "Please select a JPG, JPEG, PNG, or WebP image.");

            return;
        }

        try
        {
            await using var stream =
                File.OpenRead(path);

            var bitmap =
                new Bitmap(stream);

            _selectedCoverPath =
                path;

            SelectedCoverImage.Source =
                bitmap;

            SelectedCoverTitle.Text =
                Path.GetFileName(path);

            SelectedCoverAuthor.Text =
                "Local image";
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(
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

    private async void Create_Click(
        object? sender,
        RoutedEventArgs e)
    {
        await CreateDocumentAsync();
    }

    private async Task CreateDocumentAsync()
    {
        var title =
            TitleTextBox.Text?.Trim()
            ?? "";

        if (string.IsNullOrWhiteSpace(title))
        {
            await ShowMessageAsync(
                "Title is required.");

            return;
        }

        var documentType =
            GetSelectedDocumentType();

        try
        {
            SetInputEnabled(false);

            string? coverS3Key = null;

            if (!string.IsNullOrWhiteSpace(
                    _selectedCoverPath))
            {
                coverS3Key =
                    await UploadLocalCoverAsync(
                        _selectedCoverPath);
            }

            DateTime? publicationDate = null;

            var publicationDateText =
                PublicationDateTextBox.Text?.Trim()
                ?? "";

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
                    await ShowMessageAsync(
                        "Publication Date must be in yyyy-MM-dd format.");

                    return;
                }

                publicationDate =
                    parsedDate.Date;
            }

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

            var authorOrder = 1;

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
                        Name = name,
                        Order = authorOrder++
                    });
            }

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
                        Name = name
                    });
            }

            var doi = "";
            var journalName = "";
            var volume = "";
            var issue = "";
            var pages = "";

            if (string.Equals(
                    documentType,
                    DocumentTypes.Journal,
                    StringComparison.Ordinal))
            {
                doi =
                    DoiTextBox.Text?.Trim()
                    ?? "";

                journalName =
                    JournalNameTextBox.Text?.Trim()
                    ?? "";

                volume =
                    VolumeTextBox.Text?.Trim()
                    ?? "";

                issue =
                    IssueTextBox.Text?.Trim()
                    ?? "";

                pages =
                    PagesTextBox.Text?.Trim()
                    ?? "";
            }

            var document =
                new Document
                {
                    Title = title,

                    Authors = authors,

                    Category =
                        CategoryTextBox.Text?.Trim()
                        ?? "",

                    DocumentType =
                        documentType,

                    DOI = doi,

                    JournalName =
                        journalName,

                    Volume =
                        volume,

                    Issue =
                        issue,

                    Pages =
                        pages,

                    Tags = tags,

                    PublicationDate =
                        publicationDate,

                    CoverS3Key =
                        coverS3Key ?? ""
                };

            await _repository.CreateAsync(
                document);

            Close(true);
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(
                $"Failed to create document.\n\n{ex.Message}");
        }
        finally
        {
            SetInputEnabled(true);
        }
    }

    private string GetSelectedDocumentType()
    {
        if (DocumentTypeComboBox.SelectedItem
            is ComboBoxItem item)
        {
            return item.Tag?.ToString()
                   ?? DocumentTypes.Book;
        }

        return DocumentTypes.Book;
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

    private void SetInputEnabled(
        bool enabled)
    {
        DocumentTypeComboBox.IsEnabled =
            enabled;

        TitleTextBox.IsEnabled =
            enabled;

        AuthorTextBox.IsEnabled =
            enabled;

        CategoryTextBox.IsEnabled =
            enabled;

        TagsTextBox.IsEnabled =
            enabled;

        PublicationDateTextBox.IsEnabled =
            enabled;

        CrossrefTitleTextBox.IsEnabled =
            enabled;

        CrossrefAuthorTextBox.IsEnabled =
            enabled;

        CrossrefSearchButton.IsEnabled =
            enabled;

        CrossrefResultsListBox.IsEnabled =
            enabled;

        JournalNameTextBox.IsEnabled =
            enabled;

        VolumeTextBox.IsEnabled =
            enabled;

        IssueTextBox.IsEnabled =
            enabled;

        PagesTextBox.IsEnabled =
            enabled;

        DoiTextBox.IsEnabled =
            enabled;

        UploadCoverButton.IsEnabled =
            enabled;
    }

    private void Cancel_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Close(false);
    }

    private async Task ShowMessageAsync(
        string message)
    {
        var window = new Window
        {
            Title = "Claudel",
            Width = 420,
            Height = 180,
            WindowStartupLocation =
                WindowStartupLocation.CenterOwner
        };

        var button = new Button
        {
            Content = "OK",
            Width = 80,
            HorizontalAlignment =
                Avalonia.Layout.HorizontalAlignment.Right
        };

        button.Click += (_, _) =>
        {
            window.Close();
        };

        window.Content =
            new StackPanel
            {
                Margin =
                    new Thickness(16),

                Spacing = 12,

                Children =
                {
                    new TextBlock
                    {
                        Text = message,

                        TextWrapping =
                            Avalonia.Media.TextWrapping.Wrap
                    },

                    button
                }
            };

        await window.ShowDialog(this);
    }
}
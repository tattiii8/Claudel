using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    private readonly OpenLibraryService _openLibraryService;
    private readonly S3Service _s3Service;

    private readonly ObservableCollection<BookCoverCandidate>
        _coverCandidates = new();

    private string? _selectedPdfPath;

    private BookCoverCandidate? _selectedCover;

    private string? _selectedCoverPath;

    public NewDocumentWindow(
        DocumentRepository repository,
        AppSettings settings)
    {
        InitializeComponent();

        _repository = repository;

        _openLibraryService = new OpenLibraryService();
        _s3Service = new S3Service(settings);

        CoverCandidatesListBox.ItemsSource =
            _coverCandidates;
    }

    /*
     * Web cover search
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
            await ShowMessageAsync(
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

            SelectedCoverImage.Source = null;
            SelectedCoverTitle.Text = "";
            SelectedCoverAuthor.Text = "";

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
                await ShowMessageAsync(
                    "No cover candidates were found.");
            }
        }
        catch (Exception ex)
        {
            await ShowMessageAsync(
                $"Cover search failed.\n\n{ex.Message}");
        }
        finally
        {
            SearchCoverButton.IsEnabled = true;
            SearchCoverButton.Content = "Search Web";
        }
    }

    /*
     * Select web cover
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

        _selectedCover = candidate;

        _selectedCoverPath = null;

        SelectedCoverTitle.Text =
            candidate.Title;

        SelectedCoverAuthor.Text =
            candidate.Author;

        if (string.IsNullOrWhiteSpace(
                candidate.CoverUrl))
        {
            SelectedCoverImage.Source = null;
            return;
        }

        try
        {
            using var httpClient =
                new System.Net.Http.HttpClient();

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Claudel/1.0 (book management application)");

            var bytes =
                await httpClient.GetByteArrayAsync(
                    candidate.CoverUrl);

            await using var stream =
                new MemoryStream(bytes);

            SelectedCoverImage.Source =
                new Bitmap(stream);
        }
        catch
        {
            SelectedCoverImage.Source = null;
        }
    }

    /*
     * Upload local cover image
     */
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

            _selectedCover = null;

            CoverCandidatesListBox.SelectedItem = null;

            _selectedCoverPath = path;

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

    /*
     * Select PDF
     */
    private async void SelectPdf_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var files =
            await StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Select PDF",
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
                "The selected file could not be accessed.");

            return;
        }

        if (!string.Equals(
                Path.GetExtension(path),
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            await ShowMessageAsync(
                "Please select a PDF file.");

            return;
        }

        _selectedPdfPath = path;

        PdfFileTextBlock.Text =
            Path.GetFileName(path);
    }

    /*
     * Create document
     */
    private async void Create_Click(
        object? sender,
        RoutedEventArgs e)
    {
        await CreateDocumentAsync();
    }

    private async Task CreateDocumentAsync()
    {
        var title =
            TitleTextBox.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(title))
        {
            await ShowMessageAsync(
                "Title is required.");

            return;
        }

        try
        {
            SetInputEnabled(false);

            string? pdfS3Key = null;

            string? coverS3Key = null;

            /*
             * PDF
             */
            if (!string.IsNullOrWhiteSpace(
                    _selectedPdfPath))
            {
                pdfS3Key =
                    $"pdf/{Guid.NewGuid():N}.pdf";

                await _s3Service.UploadPdfAsync(
                    _selectedPdfPath,
                    pdfS3Key);
            }

            /*
             * Cover
             *
             * Priority:
             * 1. Local image
             * 2. Open Library
             */
            if (!string.IsNullOrWhiteSpace(
                    _selectedCoverPath))
            {
                coverS3Key =
                    await UploadLocalCoverAsync(
                        _selectedCoverPath);
            }
            else if (_selectedCover != null &&
                     !string.IsNullOrWhiteSpace(
                         _selectedCover.CoverUrl))
            {
                coverS3Key =
                    await UploadWebCoverAsync(
                        _selectedCover);
            }

            /*
             * Publication date
             */
            DateTime? publicationDate = null;

            var publicationDateText =
                PublicationDateTextBox.Text?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(
                    publicationDateText))
            {
                if (!DateTime.TryParseExact(
                        publicationDateText,
                        "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out var parsedDate))
                {
                    await ShowMessageAsync(
                        "Publication Date must be in yyyy-MM-dd format.");

                    return;
                }

                publicationDate =
                    parsedDate.Date;
            }

            /*
             * Authors
             */
            var authors =
                new List<Author>();

            var authorLines =
                (AuthorTextBox.Text ?? "")
                    .Split(
                        new[] { "\r\n", "\n", "\r" },
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

            /*
             * Tags
             */
            var tags =
                new List<Tag>();

            var tagNames =
                (TagsTextBox.Text ?? "")
                    .Split(
                        new[] { ',', '、', '\r', '\n' },
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

            var document =
                new Document
                {
                    Title = title,

                    Authors = authors,

                    Category =
                        CategoryTextBox.Text?.Trim() ?? "",

                    Tags = tags,

                    PublicationDate =
                        publicationDate,

                    S3Key =
                        pdfS3Key ?? "",

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

    /*
     * Upload local cover to S3
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
     * Enable / disable controls
     */
    private void SetInputEnabled(
        bool enabled)
    {
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

        SearchCoverButton.IsEnabled =
            enabled;

        UploadCoverButton.IsEnabled =
            enabled;

        CoverCandidatesListBox.IsEnabled =
            enabled;

        PdfFileTextBlock.IsEnabled =
            enabled;
    }

    /*
     * Cancel
     */
    private void Cancel_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Close(false);
    }

    /*
     * Message dialog
     */
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

        window.Content = new StackPanel
        {
            Margin = new Thickness(16),
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
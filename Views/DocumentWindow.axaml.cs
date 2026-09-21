using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Claudel.Models;
using Claudel.Repositories;
using Claudel.Services;

namespace Claudel;

public partial class DocumentWindow : Window
{
    private readonly DocumentRepository _repository;

    private readonly S3Service _s3Service;

    private readonly AppSettings _settings;

    private Document _document;

    public DocumentWindow(
        Document document,
        AppSettings settings,
        DocumentRepository repository)
    {
        InitializeComponent();

        _document =
            document;

        _settings =
            settings;

        _repository =
            repository;

        _s3Service =
            new S3Service(settings);

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        TitleTextBlock.Text =
            _document.Title;

        AuthorTextBlock.Text =
            _document.Author;

        CategoryTextBlock.Text =
            _document.Category;

        TagsTextBlock.Text =
            _document.Tags;

        YearTextBlock.Text =
            _document.Year?.ToString() ?? "";
    }

    private async void OpenPdf_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(
                _document.S3Key))
        {
            await ShowErrorAsync(
                "このDocumentにはPDFが登録されていません。");

            return;
        }

        try
        {
            var filePath =
                await _s3Service.DownloadPdfAsync(
                    _document.S3Key);

            var processStartInfo =
                new ProcessStartInfo
                {
                    FileName =
                        filePath,

                    UseShellExecute =
                        true
                };

            Process.Start(
                processStartInfo);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(
                $"PDFを開けませんでした。\n\n{ex.Message}");
        }
    }

    private async void Edit_Click(
        object? sender,
        RoutedEventArgs e)
    {
        try
        {
            var editWindow =
                new DocumentEditWindow(
                    _document,
                    _repository,
                    _settings);

            await editWindow.ShowDialog(this);

            var updatedDocument =
                await _repository.GetByIdAsync(
                    _document.Id);

            if (updatedDocument == null)
            {
                await ShowErrorAsync(
                    "文書が見つかりません。");

                Close();

                return;
            }

            _document =
                updatedDocument;

            UpdateDisplay();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(
                $"文書の編集に失敗しました。\n\n{ex.Message}");
        }
    }

    private async void Delete_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var confirmed =
            await ShowDeleteConfirmationAsync();

        if (!confirmed)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(
                    _document.S3Key))
            {
                await _s3Service.DeleteAsync(
                    _document.S3Key);
            }

            var deleted =
                await _repository.DeleteAsync(
                    _document.Id);

            if (!deleted)
            {
                await ShowErrorAsync(
                    "文書を削除できませんでした。");

                return;
            }

            Close();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(
                $"文書の削除に失敗しました。\n\n{ex.Message}");
        }
    }

    private async Task<bool>
        ShowDeleteConfirmationAsync()
    {
        var result =
            false;

        var dialog =
            new Window
            {
                Title =
                    "Delete Document",

                Width =
                    420,

                Height =
                    220,

                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner
            };

        var message =
            new TextBlock
            {
                Text =
                    $"「{_document.Title}」を削除しますか？\n\nPDFもS3から削除されます。この操作は元に戻せません。",

                TextWrapping =
                    Avalonia.Media.TextWrapping.Wrap,

                Margin =
                    new Avalonia.Thickness(20)
            };

        var cancelButton =
            new Button
            {
                Content =
                    "Cancel",

                Padding =
                    new Avalonia.Thickness(
                        14,
                        7)
            };

        var deleteButton =
            new Button
            {
                Content =
                    "Delete",

                Padding =
                    new Avalonia.Thickness(
                        14,
                        7)
            };

        cancelButton.Click +=
            (_, _) =>
            {
                result =
                    false;

                dialog.Close();
            };

        deleteButton.Click +=
            (_, _) =>
            {
                result =
                    true;

                dialog.Close();
            };

        var buttons =
            new StackPanel
            {
                Orientation =
                    Avalonia.Layout.Orientation.Horizontal,

                HorizontalAlignment =
                    Avalonia.Layout.HorizontalAlignment.Right,

                Spacing =
                    10,

                Children =
                {
                    cancelButton,
                    deleteButton
                }
            };

        var layout =
            new StackPanel
            {
                Spacing =
                    20,

                Children =
                {
                    message,
                    buttons
                }
            };

        dialog.Content =
            layout;

        await dialog.ShowDialog(this);

        return result;
    }

    private void Close_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Close();
    }

    private async Task ShowErrorAsync(
        string message)
    {
        var dialog =
            new Window
            {
                Title =
                    "Information",

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


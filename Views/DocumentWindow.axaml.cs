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

        UpdateRedmineDisplay();
    }

    private void UpdateRedmineDisplay()
    {
        RedmineProjectTextBlock.Text =
            _settings.Redmine.ProjectId;

        RedmineTrackerTextBlock.Text =
            _settings.Redmine.TrackerId == 5
                ? "Lecture"
                : _settings.Redmine.TrackerId.ToString();

        var issueExists =
            _document.RedmineIssueId.HasValue &&
            _document.RedmineIssueId.Value > 0 &&
            !string.IsNullOrWhiteSpace(
                _document.RedmineIssueUrl);

        if (issueExists)
        {
            RedmineIssueTextBlock.Text =
                $"#{_document.RedmineIssueId.Value}";
        }
        else
        {
            RedmineIssueTextBlock.Text =
                "Not linked";
        }

        CreateIssueButton.IsEnabled =
            !issueExists;

        LinkIssueButton.IsEnabled =
            !issueExists;

        OpenRedmineButton.IsEnabled =
            issueExists;

        UnlinkIssueButton.IsEnabled =
            issueExists;
    }


    private async void CreateIssue_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (HasLinkedIssue())
        {
            await ShowErrorAsync(
                "このDocumentには既にRedmine Issueが紐付いています.");

            return;
        }

        if (string.IsNullOrWhiteSpace(
                _document.Title))
        {
            await ShowErrorAsync(
                "タイトルが設定されていません。");

            return;
        }

        if (string.IsNullOrWhiteSpace(
                _document.Author))
        {
            await ShowErrorAsync(
                "著者が設定されていません。");

            return;
        }

        try
        {
            SetRedmineButtonsEnabled(false);

            var redmineService =
                new RedmineService(
                    _settings.Redmine);

            var result =
                await redmineService.CreateIssueAsync(
                    _document.Title,
                    _document.Author);

            _document.RedmineIssueId =
                result.Id;

            _document.RedmineIssueUrl =
                result.Url;

            var saved =
                await _repository.UpdateAsync(
                    _document);

            if (!saved)
            {
                await ShowErrorAsync(
                    "Redmine Issueは作成されましたが、" +
                    "Documentへの保存に失敗しました。\n\n" +
                    $"Issue: {result.Url}");

                return;
            }

            UpdateRedmineDisplay();

            await ShowMessageAsync(
                "Redmine Issueを作成しました。\n\n" +
                $"Issue #{result.Id}\n" +
                result.Url);
        }
        catch (Exception ex)
        {
            UpdateRedmineDisplay();

            await ShowErrorAsync(
                $"Redmine Issueの作成に失敗しました。\n\n" +
                ex.Message);
        }
    }


    private async void LinkIssue_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (HasLinkedIssue())
        {
            await ShowErrorAsync(
                "このDocumentには既にRedmine Issueが紐付いています。");

            return;
        }

        var issueId =
            await ShowIssueIdDialogAsync();

        if (!issueId.HasValue)
        {
            return;
        }

        try
        {
            SetRedmineButtonsEnabled(false);

            var redmineService =
                new RedmineService(
                    _settings.Redmine);

            var issue =
                await redmineService.GetIssueAsync(
                    issueId.Value);

            if (!string.Equals(
                    issue.ProjectIdentifier,
                    _settings.Redmine.ProjectId,
                    StringComparison.OrdinalIgnoreCase))
            {
                await ShowErrorAsync(
                    "このIssueは現在のRedmine Projectとは異なります。\n\n" +
                    $"Project: {issue.ProjectIdentifier}\n" +
                    $"Expected: {_settings.Redmine.ProjectId}");

                return;
            }

            if (issue.TrackerId !=
                _settings.Redmine.TrackerId)
            {
                await ShowErrorAsync(
                    "このIssueはLecture Trackerではありません。\n\n" +
                    $"Tracker ID: {issue.TrackerId}\n" +
                    $"Expected: {_settings.Redmine.TrackerId}");

                return;
            }

            _document.RedmineIssueId =
                issue.Id;

            _document.RedmineIssueUrl =
                issue.Url;

            var saved =
                await _repository.UpdateAsync(
                    _document);

            if (!saved)
            {
                await ShowErrorAsync(
                    "Issueは確認できましたが、" +
                    "Documentへの保存に失敗しました。");

                return;
            }

            UpdateRedmineDisplay();

            await ShowMessageAsync(
                $"Redmine Issue #{issue.Id} をDocumentに紐付けました。\n\n" +
                issue.Subject);
        }
        catch (Exception ex)
        {
            UpdateRedmineDisplay();

            await ShowErrorAsync(
                $"Issueの紐付けに失敗しました。\n\n" +
                ex.Message);
        }
    }


    private async void OpenRedmine_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(
                _document.RedmineIssueUrl))
        {
            await ShowErrorAsync(
                "Redmine Issueが紐付いていません。");

            return;
        }

        try
        {
            var processStartInfo =
                new ProcessStartInfo
                {
                    FileName =
                        _document.RedmineIssueUrl,

                    UseShellExecute =
                        true
                };

            Process.Start(
                processStartInfo);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(
                $"Redmine Issueを開けませんでした。\n\n" +
                ex.Message);
        }
    }


    private async void UnlinkIssue_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (!HasLinkedIssue())
        {
            return;
        }

        var confirmed =
            await ShowUnlinkConfirmationAsync();

        if (!confirmed)
        {
            return;
        }

        try
        {
            SetRedmineButtonsEnabled(false);

            _document.RedmineIssueId =
                null;

            _document.RedmineIssueUrl =
                "";

            var saved =
                await _repository.UpdateAsync(
                    _document);

            if (!saved)
            {
                await ShowErrorAsync(
                    "Redmine Issueの紐付けを解除できませんでした。");

                return;
            }

            UpdateRedmineDisplay();

            await ShowMessageAsync(
                "Redmine Issueの紐付けを解除しました。\n\n" +
                "Redmine側のIssueは削除されていません。");
        }
        catch (Exception ex)
        {
            UpdateRedmineDisplay();

            await ShowErrorAsync(
                $"Issueの紐付け解除に失敗しました。\n\n" +
                ex.Message);
        }
    }


    private bool HasLinkedIssue()
    {
        return
            _document.RedmineIssueId.HasValue &&
            _document.RedmineIssueId.Value > 0 &&
            !string.IsNullOrWhiteSpace(
                _document.RedmineIssueUrl);
    }


    private void SetRedmineButtonsEnabled(
        bool enabled)
    {
        if (!enabled)
        {
            CreateIssueButton.IsEnabled =
                false;

            LinkIssueButton.IsEnabled =
                false;

            OpenRedmineButton.IsEnabled =
                false;

            UnlinkIssueButton.IsEnabled =
                false;

            return;
        }

        UpdateRedmineDisplay();
    }


    private async Task<int?>
        ShowIssueIdDialogAsync()
    {
        var dialog =
            new Window
            {
                Title =
                    "Link Issue",

                Width =
                    420,

                Height =
                    230,

                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner
            };

        var issueIdTextBox =
            new TextBox
            {
                Watermark =
                    "例: 123",

                Margin =
                    new Avalonia.Thickness(
                        20,
                        0)
            };

        var result =
            (int?)null;

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

        var linkButton =
            new Button
            {
                Content =
                    "Link Issue",

                Padding =
                    new Avalonia.Thickness(
                        14,
                        7)
            };

        cancelButton.Click +=
            (_, _) =>
            {
                dialog.Close();
            };

        linkButton.Click +=
            async (_, _) =>
            {
                if (!int.TryParse(
                        issueIdTextBox.Text,
                        out var issueId) ||
                    issueId <= 0)
                {
                    await ShowDialogMessageAsync(
                        dialog,
                        "Issue IDは正の整数で入力してください。");

                    return;
                }

                result =
                    issueId;

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
                    8,

                Children =
                {
                    cancelButton,
                    linkButton
                }
            };

        var layout =
            new StackPanel
            {
                Spacing =
                    14,

                Children =
                {
                    new TextBlock
                    {
                        Text =
                            "Redmine Issue ID",

                        FontSize =
                            13,

                        FontWeight =
                            Avalonia.Media.FontWeight.SemiBold,

                        Margin =
                            new Avalonia.Thickness(
                                20,
                                20,
                                20,
                                0)
                    },

                    new TextBlock
                    {
                        Text =
                            "既存のLecture IssueのIDを入力してください。",

                        FontSize =
                            12,

                        Opacity =
                            0.6,

                        Margin =
                            new Avalonia.Thickness(
                                20,
                                0)
                    },

                    issueIdTextBox,

                    buttons
                }
            };

        dialog.Content =
            layout;

        await dialog.ShowDialog(this);

        return result;
    }


    private async Task<bool>
        ShowUnlinkConfirmationAsync()
    {
        var result =
            false;

        var dialog =
            new Window
            {
                Title =
                    "Unlink Issue",

                Width =
                    440,

                Height =
                    220,

                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner
            };

        var message =
            new TextBlock
            {
                Text =
                    $"Redmine Issue #{_document.RedmineIssueId}との紐付けを解除しますか？\n\n" +
                    "Redmine側のIssueは削除されません。",

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

        var unlinkButton =
            new Button
            {
                Content =
                    "Unlink",

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

        unlinkButton.Click +=
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
                    unlinkButton
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


    private async Task
        ShowDialogMessageAsync(
            Window owner,
            string message)
    {
        var dialog =
            new Window
            {
                Title =
                    "Information",

                Width =
                    420,

                Height =
                    180,

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

        await dialog.ShowDialog(owner);
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
                $"文書の編集に失敗しました。\n\n" +
                ex.Message);
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
                $"文書の削除に失敗しました。\n\n" +
                ex.Message);
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
                    $"「{_document.Title}」を削除しますか？\n\n" +
                    "PDFもS3から削除されます。この操作は元に戻せません。",

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


    private async Task
        ShowMessageAsync(
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
                    220,

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


    private async Task
        ShowErrorAsync(
            string message)
    {
        await ShowMessageAsync(
            message);
    }
}
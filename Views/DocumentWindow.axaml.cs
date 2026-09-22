using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Claudel.Models;
using Claudel.Repositories;
using Claudel.Services;

namespace Claudel;

public partial class DocumentWindow : Window
{
    private readonly DocumentRepository _repository;

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

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        TitleTextBlock.Text =
            _document.Title;

        var authors =
            _document.Authors
                .OrderBy(x => x.Order)
                .Select(x => x.Name)
                .ToList();

        AuthorTextBlock.Text =
            string.Join(
                ", ",
                authors);

        AuthorsTextBlock.Text =
            string.Join(
                ", ",
                authors);

        CategoryTextBlock.Text =
            _document.Category;

        TagsTextBlock.Text =
            string.Join(
                ", ",
                _document.Tags
                    .Select(x => x.Name));

        PublicationDateTextBlock.Text =
            _document.PublicationDate?
                .ToString("yyyy-MM-dd")
            ?? "";

        UpdateRedmineDisplay();

        UpdateKavitaDisplay();
    }

    /*
     * Redmine
     */
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

    /*
     * Kavita
     */
    private void UpdateKavitaDisplay()
    {
        var libraryLinked =
            _document.KavitaLibraryId.HasValue &&
            _document.KavitaLibraryId.Value > 0;

        var seriesLinked =
            _document.KavitaSeriesId.HasValue &&
            _document.KavitaSeriesId.Value > 0;

        var volumeLinked =
            _document.KavitaVolumeId.HasValue &&
            _document.KavitaVolumeId.Value > 0;

        if (libraryLinked)
        {
            KavitaLibraryTextBlock.Text =
                _document.KavitaLibraryId!.Value.ToString();
        }
        else
        {
            KavitaLibraryTextBlock.Text =
                "Not linked";
        }

        if (seriesLinked)
        {
            KavitaSeriesTextBlock.Text =
                _document.KavitaSeriesId!.Value.ToString();
        }
        else
        {
            KavitaSeriesTextBlock.Text =
                "Not linked";
        }

        if (volumeLinked)
        {
            KavitaVolumeTextBlock.Text =
                _document.KavitaVolumeId!.Value.ToString();
        }
        else
        {
            KavitaVolumeTextBlock.Text =
                "Not linked";
        }

        if (!string.IsNullOrWhiteSpace(
                _document.KavitaUrl))
        {
            KavitaUrlTextBlock.Text =
                _document.KavitaUrl;
        }
        else
        {
            KavitaUrlTextBlock.Text =
                "Not linked";
        }

        OpenKavitaButton.IsEnabled =
            !string.IsNullOrWhiteSpace(
                _document.KavitaUrl);

        LinkKavitaButton.Content =
            volumeLinked
                ? "Kavitaを再リンク"
                : "Kavitaへ同期";

        LinkKavitaButton.IsEnabled =
            true;
    }

    /*
     * Link document to Kavita.
     *
     * Claudel does not upload or move PDF files.
     *
     * The existing Kavita Library / Series / Volume
     * is selected and only its IDs and URL are stored
     * in Claudel.
     */
    private async void LinkKavita_Click(
        object? sender,
        RoutedEventArgs e)
    {
        try
        {
            SetKavitaButtonsEnabled(false);

            var kavitaService =
                new KavitaService(
                    _settings.Kavita);

            var libraries =
                await kavitaService.GetLibrariesAsync();

            if (libraries.Count == 0)
            {
                await ShowErrorAsync(
                    "KavitaにLibraryがありません。");

                return;
            }

            var configuredLibrary =
                libraries.FirstOrDefault(
                    x =>
                        x.Id ==
                        _settings.Kavita.LibraryId);

            if (configuredLibrary == null)
            {
                await ShowErrorAsync(
                    "設定されているKavita Libraryが見つかりません。\n\n" +
                    $"Configured Library ID: {_settings.Kavita.LibraryId}");
                
                return;
            }

            var series =
                await kavitaService.SearchSeriesAsync(
                    _document.Title);

            if (series.Count == 0)
            {
                await ShowErrorAsync(
                    "KavitaでこのDocumentに一致するSeriesが見つかりませんでした。\n\n" +
                    $"Title: {_document.Title}");

                return;
            }

            var selectedSeries =
                await ShowKavitaSeriesDialogAsync(
                    series);

            if (selectedSeries == null)
            {
                return;
            }

            var volumes =
                await kavitaService.GetVolumesAsync(
                    selectedSeries.Id);

            if (volumes.Count == 0)
            {
                await ShowErrorAsync(
                    "選択したSeriesにVolumeがありません。\n\n" +
                    $"Series: {selectedSeries.Name}");

                return;
            }

            var selectedVolume =
                await ShowKavitaVolumeDialogAsync(
                    volumes);

            if (selectedVolume == null)
            {
                return;
            }

            var kavitaUrl =
                kavitaService.BuildVolumeUrl(
                    configuredLibrary.Id,
                    selectedSeries.Id,
                    selectedVolume.Id);

            _document.KavitaLibraryId =
                configuredLibrary.Id;

            _document.KavitaSeriesId =
                selectedSeries.Id;

            _document.KavitaVolumeId =
                selectedVolume.Id;

            _document.KavitaUrl =
                kavitaUrl;

            var saved =
                await _repository.UpdateAsync(
                    _document);

            if (!saved)
            {
                await ShowErrorAsync(
                    "Kavitaとの紐付け情報を保存できませんでした。");

                return;
            }

            UpdateKavitaDisplay();

            await ShowMessageAsync(
                "Kavitaとの紐付けが完了しました。\n\n" +
                $"Library: {configuredLibrary.Name} (ID: {configuredLibrary.Id})\n" +
                $"Series: {selectedSeries.Name} (ID: {selectedSeries.Id})\n" +
                $"Volume: {selectedVolume} (ID: {selectedVolume.Id})");
        }
        catch (Exception ex)
        {
            UpdateKavitaDisplay();

            await ShowErrorAsync(
                $"Kavitaとの紐付けに失敗しました。\n\n" +
                ex.Message);
        }
        finally
        {
            SetKavitaButtonsEnabled(true);
        }
    }

    private void SetKavitaButtonsEnabled(
        bool enabled)
    {
        LinkKavitaButton.IsEnabled =
            enabled;

        if (enabled)
        {
            UpdateKavitaDisplay();
        }
        else
        {
            OpenKavitaButton.IsEnabled =
                false;
        }
    }

    private async Task<KavitaSeries?>
        ShowKavitaSeriesDialogAsync(
            System.Collections.Generic.List<KavitaSeries> series)
    {
        KavitaSeries? result =
            null;

        var dialog =
            new Window
            {
                Title =
                    "Kavita Seriesを選択",

                Width =
                    560,

                Height =
                    440,

                MinWidth =
                    480,

                MinHeight =
                    360,

                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner
            };

        var header =
            new StackPanel
            {
                Spacing =
                    6,

                Margin =
                    new Thickness(
                        20,
                        20,
                        20,
                        10),

                Children =
                {
                    new TextBlock
                    {
                        Text =
                            "Kavita Series",

                        FontSize =
                            16,

                        FontWeight =
                            FontWeight.SemiBold
                    },

                    new TextBlock
                    {
                        Text =
                            $"「{_document.Title}」に一致するSeriesを選択してください。",

                        FontSize =
                            12,

                        Opacity =
                            0.65,

                        TextWrapping =
                            TextWrapping.Wrap
                    }
                }
            };

        var listBox =
            new ListBox
            {
                ItemsSource =
                    series,

                Margin =
                    new Thickness(
                        20,
                        0,
                        20,
                        10),

            };

        if (series.Count > 0)
        {
            listBox.SelectedIndex =
                0;
        }

        var cancelButton =
            new Button
            {
                Content =
                    "Cancel",

                Padding =
                    new Thickness(
                        14,
                        7)
            };

        var selectButton =
            new Button
            {
                Content =
                    "Select",

                Padding =
                    new Thickness(
                        14,
                        7)
            };

        cancelButton.Click +=
            (_, _) =>
            {
                dialog.Close();
            };

        selectButton.Click +=
            async (_, _) =>
            {
                if (listBox.SelectedItem
                    is not KavitaSeries selected)
                {
                    await ShowDialogMessageAsync(
                        dialog,
                        "Seriesを選択してください。");

                    return;
                }

                result =
                    selected;

                dialog.Close();
            };

        var buttons =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                Spacing =
                    8,

                Margin =
                    new Thickness(
                        20,
                        0,
                        20,
                        20),

                Children =
                {
                    cancelButton,
                    selectButton
                }
            };

        var grid =
            new Grid
            {
                RowDefinitions =
                    new RowDefinitions(
                        "Auto,*,Auto")
            };

        grid.Children.Add(
            header);

        grid.Children.Add(
            listBox);

        grid.Children.Add(
            buttons);

        Grid.SetRow(
            listBox,
            1);

        Grid.SetRow(
            buttons,
            2);

        dialog.Content =
            grid;

        await dialog.ShowDialog(
            this);

        return result;
    }

    private async Task<KavitaVolume?>
        ShowKavitaVolumeDialogAsync(
            System.Collections.Generic.List<KavitaVolume> volumes)
    {
        KavitaVolume? result =
            null;

        var dialog =
            new Window
            {
                Title =
                    "Kavita Volumeを選択",

                Width =
                    560,

                Height =
                    440,

                MinWidth =
                    480,

                MinHeight =
                    360,

                WindowStartupLocation =
                    WindowStartupLocation.CenterOwner
            };

        var header =
            new StackPanel
            {
                Spacing =
                    6,

                Margin =
                    new Thickness(
                        20,
                        20,
                        20,
                        10),

                Children =
                {
                    new TextBlock
                    {
                        Text =
                            "Kavita Volume",

                        FontSize =
                            16,

                        FontWeight =
                            FontWeight.SemiBold
                    },

                    new TextBlock
                    {
                        Text =
                            "このDocumentに対応するVolumeを選択してください。",

                        FontSize =
                            12,

                        Opacity =
                            0.65,

                        TextWrapping =
                            TextWrapping.Wrap
                    }
                }
            };

        var listBox =
            new ListBox
            {
                ItemsSource =
                    volumes,

                Margin =
                    new Thickness(
                        20,
                        0,
                        20,
                        10),
            };

        if (volumes.Count > 0)
        {
            listBox.SelectedIndex =
                0;
        }

        var cancelButton =
            new Button
            {
                Content =
                    "Cancel",

                Padding =
                    new Thickness(
                        14,
                        7)
            };

        var selectButton =
            new Button
            {
                Content =
                    "Select",

                Padding =
                    new Thickness(
                        14,
                        7)
            };

        cancelButton.Click +=
            (_, _) =>
            {
                dialog.Close();
            };

        selectButton.Click +=
            async (_, _) =>
            {
                if (listBox.SelectedItem
                    is not KavitaVolume selected)
                {
                    await ShowDialogMessageAsync(
                        dialog,
                        "Volumeを選択してください。");

                    return;
                }

                result =
                    selected;

                dialog.Close();
            };

        var buttons =
            new StackPanel
            {
                Orientation =
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

                Spacing =
                    8,

                Margin =
                    new Thickness(
                        20,
                        0,
                        20,
                        20),

                Children =
                {
                    cancelButton,
                    selectButton
                }
            };

        var grid =
            new Grid
            {
                RowDefinitions =
                    new RowDefinitions(
                        "Auto,*,Auto")
            };

        grid.Children.Add(
            header);

        grid.Children.Add(
            listBox);

        grid.Children.Add(
            buttons);

        Grid.SetRow(
            listBox,
            1);

        Grid.SetRow(
            buttons,
            2);

        dialog.Content =
            grid;

        await dialog.ShowDialog(
            this);

        return result;
    }

    /*
     * Create Redmine Issue.
     */
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

        var authorText =
            string.Join(
                ", ",
                _document.Authors
                    .OrderBy(x => x.Order)
                    .Select(x => x.Name));

        if (string.IsNullOrWhiteSpace(
                authorText))
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
                    authorText);

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

    /*
     * Link existing Redmine Issue.
     */
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
                    "このIssueは現在設定されているTrackerとは異なります。\n\n" +
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

    /*
     * Open Redmine Issue.
     */
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

    /*
     * Unlink Redmine Issue.
     */
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

    /*
     * Open Kavita.
     */
    private async void OpenKavita_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(
                _document.KavitaUrl))
        {
            await ShowErrorAsync(
                "このDocumentにはKavitaが紐付いていません。");

            return;
        }

        try
        {
            var processStartInfo =
                new ProcessStartInfo
                {
                    FileName =
                        _document.KavitaUrl,

                    UseShellExecute =
                        true
                };

            Process.Start(
                processStartInfo);
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(
                $"Kavitaを開けませんでした。\n\n" +
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
                    new Thickness(
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
                    new Thickness(
                        14,
                        7)
            };

        var linkButton =
            new Button
            {
                Content =
                    "Link Issue",

                Padding =
                    new Thickness(
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
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

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
                            FontWeight.SemiBold,

                        Margin =
                            new Thickness(
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
                            new Thickness(
                                20,
                                0)
                    },

                    issueIdTextBox,

                    buttons
                }
            };

        dialog.Content =
            layout;

        await dialog.ShowDialog(
            this);

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
                    TextWrapping.Wrap,

                Margin =
                    new Thickness(20)
            };

        var cancelButton =
            new Button
            {
                Content =
                    "Cancel",

                Padding =
                    new Thickness(
                        14,
                        7)
            };

        var unlinkButton =
            new Button
            {
                Content =
                    "Unlink",

                Padding =
                    new Thickness(
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
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

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

        await dialog.ShowDialog(
            this);

        return result;
    }

    /*
     * Edit document.
     */
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

            await editWindow.ShowDialog(
                this);

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

    /*
     * Delete document.
     *
     * PDF/S3 document deletion has intentionally
     * been removed because Claudel no longer
     * manages PDF files.
     */
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
                    "Claudelの書誌データと関連付けを削除します。\n" +
                    "RedmineやKavita側のデータは削除されません。",

                TextWrapping =
                    TextWrapping.Wrap,

                Margin =
                    new Thickness(20)
            };

        var cancelButton =
            new Button
            {
                Content =
                    "Cancel",

                Padding =
                    new Thickness(
                        14,
                        7)
            };

        var deleteButton =
            new Button
            {
                Content =
                    "Delete",

                Padding =
                    new Thickness(
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
                    Orientation.Horizontal,

                HorizontalAlignment =
                    HorizontalAlignment.Right,

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

        await dialog.ShowDialog(
            this);

        return result;
    }

    private void Close_Click(
        object? sender,
        RoutedEventArgs e)
    {
        Close();
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
                            TextWrapping.Wrap,

                        Margin =
                            new Thickness(20)
                    }
            };

        await dialog.ShowDialog(
            owner);
    }

    private async Task
        ShowMessageAsync(
            string message)
    {
        await ShowDialogMessageAsync(
            this,
            message);
    }

    private async Task
        ShowErrorAsync(
            string message)
    {
        await ShowMessageAsync(
            message);
    }
}
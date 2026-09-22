using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Claudel.Models;

namespace Claudel.Services;

public class RedmineService
{
private readonly RedmineSettings _settings;

public RedmineService(
    RedmineSettings settings)
{
    _settings = settings;
}

private HttpClient CreateClient()
{
    if (string.IsNullOrWhiteSpace(
            _settings.Url))
    {
        throw new InvalidOperationException(
            "Redmine URL is not configured.");
    }

    if (string.IsNullOrWhiteSpace(
            _settings.ApiKey))
    {
        throw new InvalidOperationException(
            "Redmine API Key is not configured.");
    }

    var baseUrl =
        _settings.Url.TrimEnd('/') + "/";

    var client =
        new HttpClient
        {
            BaseAddress =
                new Uri(baseUrl)
        };

    client.DefaultRequestHeaders.Add(
        "X-Redmine-API-Key",
        _settings.ApiKey);

    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Claudel/1.0");

    return client;
}


public async Task TestConnectionAsync()
{
    using var client =
        CreateClient();

    using var response =
        await client.GetAsync(
            "users/current.json");

    var body =
        await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException(
            $"Redmine connection failed " +
            $"({(int)response.StatusCode}).\n\n" +
            body);
    }
}


// =========================================
// Redmineへ同期
//
// Claudel ID
// Author
// Tags
// Publication Date
// Kavita URL
//
// Custom Field IDは固定しない。
// Redmine APIから名前で取得する。
// =========================================

public async Task<RedmineIssueResult>
    SyncIssueAsync(
        Document document)
{
    if (document == null)
    {
        throw new ArgumentNullException(
            nameof(document));
    }

    if (string.IsNullOrWhiteSpace(
            _settings.ProjectId))
    {
        throw new InvalidOperationException(
            "Redmine Project ID is not configured.");
    }

    if (_settings.TrackerId <= 0)
    {
        throw new InvalidOperationException(
            "Redmine Tracker ID is invalid.");
    }

    if (document.Id <= 0)
    {
        throw new InvalidOperationException(
            "Document ID is invalid.");
    }

    if (string.IsNullOrWhiteSpace(
            document.Title))
    {
        throw new InvalidOperationException(
            "Document title is not configured.");
    }

    using var client =
        CreateClient();

    // =========================================
    // Custom Field ID取得
    // =========================================

    var customFieldIds =
        await GetCustomFieldIdsAsync(
            client);

    // =========================================
    // Custom Field構築
    // =========================================

    var customFields =
        BuildCustomFields(
            document,
            customFieldIds);

    // =========================================
    // Issue共通データ
    // =========================================

    var issuePayload =
        new
        {
            project_id =
                _settings.ProjectId.Trim(),

            tracker_id =
                _settings.TrackerId,

            subject =
                document.Title,

            custom_fields =
                customFields
        };

    // =========================================
    // 既存Issueの場合
    // =========================================

    if (document.RedmineIssueId.HasValue &&
        document.RedmineIssueId.Value > 0)
    {
        var issueId =
            document.RedmineIssueId.Value;

        var payload =
            new
            {
                issue =
                    issuePayload
            };

        var json =
            JsonSerializer.Serialize(
                payload);

        using var content =
            new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

        using var response =
            await client.PutAsync(
                $"issues/{issueId}.json",
                content);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Redmine issue update failed " +
                $"({(int)response.StatusCode}).\n\n" +
                responseBody);
        }

        var url =
            _settings.Url.TrimEnd('/') +
            "/issues/" +
            issueId;

        return new RedmineIssueResult(
            issueId,
            url,
            _settings.ProjectId,
            _settings.TrackerId,
            document.Title);
    }

    // =========================================
    // 新規Issueの場合
    // =========================================

    var createPayload =
        new
        {
            issue =
                issuePayload
        };

    var createJson =
        JsonSerializer.Serialize(
            createPayload);

    using var createContent =
        new StringContent(
            createJson,
            Encoding.UTF8,
            "application/json");

    using var createResponse =
        await client.PostAsync(
            "issues.json",
            createContent);

    var createResponseBody =
        await createResponse.Content.ReadAsStringAsync();

    if (!createResponse.IsSuccessStatusCode)
    {
        throw new InvalidOperationException(
            $"Redmine issue creation failed " +
            $"({(int)createResponse.StatusCode}).\n\n" +
            createResponseBody);
    }

    using var responseDocument =
        JsonDocument.Parse(
            createResponseBody);

    var issue =
        responseDocument.RootElement
            .GetProperty("issue");

    var id =
        issue
            .GetProperty("id")
            .GetInt32();

    var issueUrl =
        _settings.Url.TrimEnd('/') +
        "/issues/" +
        id;

    return new RedmineIssueResult(
        id,
        issueUrl,
        _settings.ProjectId,
        _settings.TrackerId,
        document.Title);
}


// =========================================
// Custom Field ID取得
//
// 名前を固定し、IDはRedmineから毎回取得する。
// =========================================

private async Task<Dictionary<string, int>>
    GetCustomFieldIdsAsync(
        HttpClient client)
{
    using var response =
        await client.GetAsync(
            "custom_fields.json");

    var responseBody =
        await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException(
            $"Redmine Custom Field lookup failed " +
            $"({(int)response.StatusCode}).\n\n" +
            responseBody);
    }

    using var document =
        JsonDocument.Parse(
            responseBody);

    var result =
        new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);

    var customFields =
        document.RootElement
            .GetProperty("custom_fields");

    foreach (var field in customFields.EnumerateArray())
    {
        var id =
            field
                .GetProperty("id")
                .GetInt32();

        var name =
            field
                .GetProperty("name")
                .GetString();

        if (!string.IsNullOrWhiteSpace(name))
        {
            result[name] = id;
        }
    }

    var requiredNames =
        new[]
        {
            "Claudel ID",
            "Author",
            "Tags",
            "Publication Date",
            "Kavita URL"
        };

    var missing =
        requiredNames
            .Where(name =>
                !result.ContainsKey(name))
            .ToList();

    if (missing.Count > 0)
    {
        throw new InvalidOperationException(
            "以下のRedmine Custom Fieldが見つかりません。\n\n" +
            string.Join(
                "\n",
                missing) +
            "\n\n" +
            "Redmine側でCustom Field名を確認してください。");
    }

    return result;
}


// =========================================
// Custom Field値構築
// =========================================

private static object[]
    BuildCustomFields(
        Document document,
        Dictionary<string, int> fieldIds)
{
    var author =
        string.Join(
            ", ",
            document.Authors
                .OrderBy(a => a.Order)
                .Select(a => a.Name)
                .Where(name =>
                    !string.IsNullOrWhiteSpace(name)));

    var tags =
        string.Join(
            ", ",
            document.Tags
                .Select(t => t.Name)
                .Where(name =>
                    !string.IsNullOrWhiteSpace(name)));

    var publicationDate =
        document.PublicationDate.HasValue
            ? document.PublicationDate.Value
                .ToString("yyyy-MM-dd")
            : "";

    return new object[]
    {
        new
        {
            id = fieldIds["Claudel ID"],
            value = document.Id.ToString()
        },

        new
        {
            id = fieldIds["Author"],
            value = author
        },

        new
        {
            id = fieldIds["Tags"],
            value = tags
        },

        new
        {
            id = fieldIds["Publication Date"],
            value = publicationDate
        },

        new
        {
            id = fieldIds["Kavita URL"],
            value = document.KavitaUrl ?? ""
        }
    };
}


// =========================================
// Issue取得
// =========================================

public async Task<RedmineIssueResult>
    GetIssueAsync(
        int issueId)
{
    if (issueId <= 0)
    {
        throw new ArgumentException(
            "Issue ID is invalid.",
            nameof(issueId));
    }

    using var client =
        CreateClient();


    // =========================================
    // Issue取得
    // =========================================

    using var issueResponse =
        await client.GetAsync(
            $"issues/{issueId}.json");

    var issueBody =
        await issueResponse.Content.ReadAsStringAsync();

    if (!issueResponse.IsSuccessStatusCode)
    {
        if ((int)issueResponse.StatusCode == 404)
        {
            throw new InvalidOperationException(
                $"Redmine Issue #{issueId} was not found.");
        }

        throw new InvalidOperationException(
            $"Redmine Issue lookup failed " +
            $"({(int)issueResponse.StatusCode}).\n\n" +
            issueBody);
    }


    using var issueDocument =
        JsonDocument.Parse(
            issueBody);

    var issue =
        issueDocument.RootElement
            .GetProperty("issue");


    // =========================================
    // Issue ID
    // =========================================

    var id =
        issue
            .GetProperty("id")
            .GetInt32();


    // =========================================
    // Issue URL
    // =========================================

    var issueUrl =
        _settings.Url.TrimEnd('/') +
        "/issues/" +
        id;


    // =========================================
    // Issue Project ID
    // =========================================

    var issueProjectId =
        issue
            .GetProperty("project")
            .GetProperty("id")
            .GetInt32();


    // =========================================
    // Issue Tracker ID
    // =========================================

    var trackerId =
        issue
            .GetProperty("tracker")
            .GetProperty("id")
            .GetInt32();


    // =========================================
    // Issue Subject
    // =========================================

    var subject =
        issue
            .GetProperty("subject")
            .GetString() ?? "";


    // =========================================
    // 設定されているRedmine Projectを取得
    //
    // 例:
    // Project ID = recherche
    //
    // GET /projects/recherche.json
    // =========================================

    using var projectResponse =
        await client.GetAsync(
            $"projects/{Uri.EscapeDataString(
                _settings.ProjectId.Trim())}.json");

    var projectBody =
        await projectResponse.Content.ReadAsStringAsync();

    if (!projectResponse.IsSuccessStatusCode)
    {
        throw new InvalidOperationException(
            $"Redmine Project lookup failed " +
            $"({(int)projectResponse.StatusCode}).\n\n" +
            projectBody);
    }


    using var projectDocument =
        JsonDocument.Parse(
            projectBody);

    var project =
        projectDocument.RootElement
            .GetProperty("project");

    var configuredProjectId =
        project
            .GetProperty("id")
            .GetInt32();


    // =========================================
    // Projectチェック
    // =========================================

    if (issueProjectId != configuredProjectId)
    {
        throw new InvalidOperationException(
            "このIssueは現在のRedmine Projectとは異なります。\n\n" +
            $"Issue Project ID: {issueProjectId}\n" +
            $"Expected Project ID: {configuredProjectId}\n\n" +
            $"Configured Project: {_settings.ProjectId}");
    }


    // =========================================
    // 結果
    // =========================================

    return new RedmineIssueResult(
        id,
        issueUrl,
        _settings.ProjectId,
        trackerId,
        subject);
}

}

public record RedmineIssueResult(
int Id,
string Url,
string ProjectIdentifier = "",
int TrackerId = 0,
string Subject = "");

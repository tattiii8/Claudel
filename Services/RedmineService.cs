using System;
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


    public async Task<RedmineIssueResult>
        CreateIssueAsync(
            string subject,
            string author)
    {
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

        using var client =
            CreateClient();

        var payload =
            new
            {
                issue = new
                {
                    project_id =
                        _settings.ProjectId.Trim(),

                    tracker_id =
                        _settings.TrackerId,

                    subject =
                        subject,

                    custom_fields =
                        new[]
                        {
                            new
                            {
                                id = 1,
                                value = author ?? ""
                            }
                        }
                }
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
            await client.PostAsync(
                "issues.json",
                content);

        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Redmine issue creation failed " +
                $"({(int)response.StatusCode}).\n\n" +
                responseBody);
        }

        using var document =
            JsonDocument.Parse(
                responseBody);

        var issue =
            document.RootElement
                .GetProperty("issue");

        var id =
            issue
                .GetProperty("id")
                .GetInt32();

        var url =
            _settings.Url.TrimEnd('/') +
            "/issues/" +
            id;

        return new RedmineIssueResult(
            id,
            url);
    }


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

        var url =
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
            url,
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
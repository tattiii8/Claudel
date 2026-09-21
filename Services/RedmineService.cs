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

        using var response =
            await client.GetAsync(
                $"issues/{issueId}.json");

        var responseBody =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode == 404)
            {
                throw new InvalidOperationException(
                    $"Redmine Issue #{issueId} was not found.");
            }

            throw new InvalidOperationException(
                $"Redmine Issue lookup failed " +
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

        var projectIdentifier =
            issue
                .GetProperty("project")
                .GetProperty("identifier")
                .GetString() ?? "";

        var trackerId =
            issue
                .GetProperty("tracker")
                .GetProperty("id")
                .GetInt32();

        var subject =
            issue
                .GetProperty("subject")
                .GetString() ?? "";

        return new RedmineIssueResult(
            id,
            url,
            projectIdentifier,
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
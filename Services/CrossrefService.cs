using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Claudel.Models;

namespace Claudel.Services;

public class CrossrefService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri("https://api.crossref.org/v1/")
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Claudel/1.0");

        return client;
    }

    public async Task<List<CrossrefWork>> SearchJournalArticlesAsync(
        string title,
        string author)
    {
        title = title?.Trim() ?? "";
        author = author?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(title) &&
            string.IsNullOrWhiteSpace(author))
        {
            return new List<CrossrefWork>();
        }

        var query = string.Join(
            " ",
            new[]
            {
                title,
                author
            }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        var url =
            $"works?query.bibliographic={Uri.EscapeDataString(query)}" +
            "&filter=type:journal-article" +
            "&rows=10";

        using var response =
            await HttpClient.GetAsync(url);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync();

        var result =
            await JsonSerializer.DeserializeAsync<CrossrefResponse>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (result?.Message?.Items == null)
        {
            return new List<CrossrefWork>();
        }

        return result.Message.Items
            .Select(MapWork)
            .Where(x => !string.IsNullOrWhiteSpace(x.Title))
            .ToList();
    }

    private static CrossrefWork MapWork(
        CrossrefItem item)
    {
        var work = new CrossrefWork
        {
            DOI =
                item.DOI ?? "",

            Title =
                item.Title?.FirstOrDefault() ?? "",

            JournalName =
                item.ContainerTitle?.FirstOrDefault() ?? "",

            Volume =
                item.Volume ?? "",

            Issue =
                item.Issue ?? "",

            Pages =
                item.Page ?? "",

            Publisher =
                item.Publisher ?? "",

            PublicationDate =
                GetPublicationDate(item)
        };

        if (item.Author != null)
        {
            foreach (var author in item.Author)
            {
                var name =
                    BuildAuthorName(author);

                if (!string.IsNullOrWhiteSpace(name))
                {
                    work.Authors.Add(name);
                }
            }
        }

        return work;
    }

    private static string BuildAuthorName(
        CrossrefAuthor author)
    {
        var name =
            string.Join(
                " ",
                new[]
                {
                    author.Given,
                    author.Family
                }
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x)));

        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return author.Name?.Trim() ?? "";
    }

    private static DateTime? GetPublicationDate(
        CrossrefItem item)
    {
        var dateParts =
            item.Published?.DateParts ??
            item.PublishedPrint?.DateParts ??
            item.PublishedOnline?.DateParts;

        if (dateParts == null ||
            dateParts.Count == 0)
        {
            return null;
        }

        var parts =
            dateParts[0];

        if (parts.Count == 0)
        {
            return null;
        }

        var year =
            parts[0];

        var month =
            parts.Count >= 2
                ? parts[1]
                : 1;

        var day =
            parts.Count >= 3
                ? parts[2]
                : 1;

        try
        {
            return new DateTime(
                year,
                month,
                day);
        }
        catch
        {
            return null;
        }
    }

    private sealed class CrossrefResponse
    {
        [JsonPropertyName("message")]
        public CrossrefMessage? Message { get; set; }
    }

    private sealed class CrossrefMessage
    {
        [JsonPropertyName("items")]
        public List<CrossrefItem>? Items { get; set; }
    }

    private sealed class CrossrefItem
    {
        [JsonPropertyName("DOI")]
        public string? DOI { get; set; }

        [JsonPropertyName("title")]
        public List<string>? Title { get; set; }

        [JsonPropertyName("author")]
        public List<CrossrefAuthor>? Author { get; set; }

        [JsonPropertyName("container-title")]
        public List<string>? ContainerTitle { get; set; }

        [JsonPropertyName("published")]
        public CrossrefDate? Published { get; set; }

        [JsonPropertyName("published-print")]
        public CrossrefDate? PublishedPrint { get; set; }

        [JsonPropertyName("published-online")]
        public CrossrefDate? PublishedOnline { get; set; }

        [JsonPropertyName("volume")]
        public string? Volume { get; set; }

        [JsonPropertyName("issue")]
        public string? Issue { get; set; }

        [JsonPropertyName("page")]
        public string? Page { get; set; }

        [JsonPropertyName("publisher")]
        public string? Publisher { get; set; }
    }

    private sealed class CrossrefAuthor
    {
        [JsonPropertyName("given")]
        public string? Given { get; set; }

        [JsonPropertyName("family")]
        public string? Family { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private sealed class CrossrefDate
    {
        [JsonPropertyName("date-parts")]
        public List<List<int>>? DateParts { get; set; }
    }
}
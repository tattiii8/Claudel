using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Claudel.Models;

namespace Claudel.Services;

public class OpenLibraryService
{
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri("https://openlibrary.org/")
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Claudel/1.0 (book management application)");

        return client;
    }

    public async Task<List<BookCoverCandidate>> SearchAsync(
        string title,
        string author)
    {
        if (string.IsNullOrWhiteSpace(title) &&
            string.IsNullOrWhiteSpace(author))
        {
            return new List<BookCoverCandidate>();
        }

        var parameters = new List<string>
        {
            "limit=12",
            "fields=key,title,author_name,first_publish_year,isbn,cover_i"
        };

        if (!string.IsNullOrWhiteSpace(title))
        {
            parameters.Add(
                $"title={Uri.EscapeDataString(title.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(author))
        {
            parameters.Add(
                $"author={Uri.EscapeDataString(author.Trim())}");
        }

        var url = "search.json?" + string.Join("&", parameters);

        using var response = await HttpClient.GetAsync(url);

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync();

        var result =
            await JsonSerializer.DeserializeAsync<SearchResponse>(
                stream,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

        if (result?.Docs == null)
        {
            return new List<BookCoverCandidate>();
        }

        return result.Docs
            .Where(x => x.CoverId.HasValue)
            .Select(x => new BookCoverCandidate
            {
                Title = x.Title ?? "",

                Author =
                    x.AuthorName?.FirstOrDefault() ?? "",

                FirstPublishYear =
                    x.FirstPublishYear,

                Isbn13 =
                    FindIsbn13(x.Isbn),

                Isbn10 =
                    FindIsbn10(x.Isbn),

                OpenLibraryKey =
                    x.Key,

                CoverUrl =
                    $"https://covers.openlibrary.org/b/id/{x.CoverId.Value}-L.jpg"
            })
            .ToList();
    }

    public async Task<string?> DownloadCoverAsync(
        BookCoverCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.CoverUrl))
        {
            return null;
        }

        using var response =
            await HttpClient.GetAsync(
                candidate.CoverUrl,
                HttpCompletionOption.ResponseHeadersRead);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"claudel-cover-{Guid.NewGuid():N}.jpg");

        await using var input =
            await response.Content.ReadAsStreamAsync();

        await using var output =
            File.Create(tempPath);

        await input.CopyToAsync(output);

        return tempPath;
    }

    private static string? FindIsbn13(
        List<string>? isbns)
    {
        return isbns?
            .FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x) &&
                x.Replace("-", "").Length == 13);
    }

    private static string? FindIsbn10(
        List<string>? isbns)
    {
        return isbns?
            .FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x) &&
                x.Replace("-", "").Length == 10);
    }

    private sealed class SearchResponse
    {
        [JsonPropertyName("docs")]
        public List<SearchDocument>? Docs { get; set; }
    }

    private sealed class SearchDocument
    {
        [JsonPropertyName("key")]
        public string? Key { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("author_name")]
        public List<string>? AuthorName { get; set; }

        [JsonPropertyName("first_publish_year")]
        public int? FirstPublishYear { get; set; }

        [JsonPropertyName("isbn")]
        public List<string>? Isbn { get; set; }

        [JsonPropertyName("cover_i")]
        public int? CoverId { get; set; }
    }
}
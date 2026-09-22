using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Claudel.Models;

namespace Claudel.Services;

public class KavitaService
{
    private readonly KavitaSettings _settings;
    private readonly HttpClient _httpClient;

    public KavitaService(
        KavitaSettings settings)
    {
        _settings =
            settings;

        _httpClient =
            new HttpClient();

        if (!string.IsNullOrWhiteSpace(
                _settings.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add(
                "x-api-key",
                _settings.ApiKey);
        }
    }

    private string BaseUrl
    {
        get
        {
            var url =
                _settings.Url.TrimEnd('/');

            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException(
                    "Kavita URL is not configured.");
            }

            if (string.IsNullOrWhiteSpace(
                    _settings.ApiKey))
            {
                throw new InvalidOperationException(
                    "Kavita API Key is not configured.");
            }

            return url;
        }
    }

    public async Task TestConnectionAsync()
    {
        using var response =
            await _httpClient.GetAsync(
                $"{BaseUrl}/api/Library/libraries");

        await EnsureSuccessAsync(
            response);
    }

    public async Task<List<KavitaLibrary>>
        GetLibrariesAsync()
    {
        using var response =
            await _httpClient.GetAsync(
                $"{BaseUrl}/api/Library/libraries");

        await EnsureSuccessAsync(
            response);

        var result =
            await response.Content
                .ReadFromJsonAsync<
                    List<KavitaLibrary>>();

        return result
            ?? new List<KavitaLibrary>();
    }

    public async Task<List<KavitaSeries>>
        SearchSeriesAsync(
            string title)
    {
        if (string.IsNullOrWhiteSpace(
                title))
        {
            return new List<KavitaSeries>();
        }

        if (_settings.LibraryId <= 0)
        {
            throw new InvalidOperationException(
                "Kavita Library ID is not configured.");
        }

        var endpoint =
            $"{BaseUrl}/api/Series/v2" +
            $"?libraryId={_settings.LibraryId}";

        /*
         * Kavitaの実際のAPIレスポンスを確認済み。
         *
         * POST /api/Series/v2?libraryId=1
         *
         * {
         *   "statements": [],
         *   "limitTo": 0,
         *   "sortOptions": null
         * }
         *
         * このAPIはLibrary内のSeries一覧を返すため、
         * タイトルによる候補抽出はClaudel側で行う。
         */
        var requestBody =
            new
            {
                statements =
                    Array.Empty<object>(),

                limitTo =
                    0,

                sortOptions =
                    (object?)null
            };

        using var response =
            await _httpClient.PostAsJsonAsync(
                endpoint,
                requestBody);

        await EnsureSuccessAsync(
            response);

        using var document =
            JsonDocument.Parse(
                await response.Content
                    .ReadAsStringAsync());

        var root =
            document.RootElement;

        var series =
            new List<KavitaSeries>();

        if (root.ValueKind !=
            JsonValueKind.Array)
        {
            return series;
        }

        foreach (var item
                 in root.EnumerateArray())
        {
            var id =
                GetInt32(
                    item,
                    "id");

            var name =
                GetString(
                    item,
                    "name");

            if (id <= 0 ||
                string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            /*
             * 現在は完全一致を優先し、
             * 完全一致しない場合は部分一致も候補にする。
             *
             * Documentのタイトル:
             *   考えながら学ぶキリスト教
             *
             * Kavita:
             *   考えながら学ぶキリスト教
             *
             * のようなケースを直接取得できる。
             */
            if (string.Equals(
                    name.Trim(),
                    title.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                series.Insert(
                    0,
                    new KavitaSeries
                    {
                        Id =
                            id,

                        Name =
                            name
                    });

                continue;
            }

            if (name.Contains(
                    title,
                    StringComparison.OrdinalIgnoreCase) ||
                title.Contains(
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                series.Add(
                    new KavitaSeries
                    {
                        Id =
                            id,

                        Name =
                            name
                    });
            }
        }

        return series;
    }

    public async Task<List<KavitaVolume>>
        GetVolumesAsync(
            int seriesId)
    {
        if (seriesId <= 0)
        {
            throw new ArgumentException(
                "Series ID is invalid.",
                nameof(seriesId));
        }

        using var response =
            await _httpClient.GetAsync(
                $"{BaseUrl}/api/Series/volumes" +
                $"?seriesId={seriesId}");

        await EnsureSuccessAsync(
            response);

        using var document =
            JsonDocument.Parse(
                await response.Content
                    .ReadAsStringAsync());

        var root =
            document.RootElement;

        var volumes =
            new List<KavitaVolume>();

        if (root.ValueKind !=
            JsonValueKind.Array)
        {
            return volumes;
        }

        foreach (var item
                 in root.EnumerateArray())
        {
            var id =
                GetInt32(
                    item,
                    "id");

            if (id <= 0)
            {
                continue;
            }

            var volume =
                new KavitaVolume
                {
                    Id =
                        id,

                    Name =
                        GetString(
                            item,
                            "name"),

                    Number =
                        GetString(
                            item,
                            "number"),

                    ChapterTitle =
                        GetFirstChapterTitle(
                            item)
                };

            volumes.Add(
                volume);
        }

        return volumes;
    }

    public string BuildVolumeUrl(
        int libraryId,
        int seriesId,
        int volumeId)
    {
        if (libraryId <= 0)
        {
            throw new ArgumentException(
                "Library ID is invalid.",
                nameof(libraryId));
        }

        if (seriesId <= 0)
        {
            throw new ArgumentException(
                "Series ID is invalid.",
                nameof(seriesId));
        }

        if (volumeId <= 0)
        {
            throw new ArgumentException(
                "Volume ID is invalid.",
                nameof(volumeId));
        }

        /*
         * KavitaのWeb UI URL。
         *
         * 例:
         * http://localhost:5000/library/1/series/1/volume/1
         */
        return
            $"{BaseUrl}/library/{libraryId}" +
            $"/series/{seriesId}" +
            $"/volume/{volumeId}";
    }

    private static string
        GetFirstChapterTitle(
            JsonElement volume)
    {
        if (!volume.TryGetProperty(
                "chapters",
                out var chapters))
        {
            return "";
        }

        if (chapters.ValueKind !=
            JsonValueKind.Array)
        {
            return "";
        }

        foreach (var chapter
                 in chapters.EnumerateArray())
        {
            var title =
                GetString(
                    chapter,
                    "title");

            if (!string.IsNullOrWhiteSpace(
                    title))
            {
                return title;
            }
        }

        return "";
    }

    private static int GetInt32(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var property))
        {
            return 0;
        }

        if (property.ValueKind ==
                JsonValueKind.Number &&
            property.TryGetInt32(
                out var value))
        {
            return value;
        }

        if (property.ValueKind ==
                JsonValueKind.String &&
            int.TryParse(
                property.GetString(),
                out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static string GetString(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(
                propertyName,
                out var property))
        {
            return "";
        }

        if (property.ValueKind ==
            JsonValueKind.String)
        {
            return property.GetString()
                ?? "";
        }

        return property.ToString();
    }

    private static async Task
        EnsureSuccessAsync(
            HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body =
            await response.Content
                .ReadAsStringAsync();

        throw new InvalidOperationException(
            $"Kavita API request failed.\n" +
            $"HTTP {(int)response.StatusCode} " +
            $"{response.ReasonPhrase}\n\n" +
            body);
    }
}

public class KavitaLibrary
{
    public int Id { get; set; }

    public string Name { get; set; } = "";
}

public class KavitaSeries
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public override string ToString()
    {
        return
            $"{Name} (ID: {Id})";
    }
}

public class KavitaVolume
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Number { get; set; } = "";

    public string ChapterTitle { get; set; } = "";

    public override string ToString()
    {
        if (!string.IsNullOrWhiteSpace(
                ChapterTitle))
        {
            return ChapterTitle;
        }

        if (!string.IsNullOrWhiteSpace(
                Name))
        {
            return Name;
        }

        if (!string.IsNullOrWhiteSpace(
                Number))
        {
            return
                $"Volume {Number}";
        }

        return
            $"Volume {Id}";
    }
}
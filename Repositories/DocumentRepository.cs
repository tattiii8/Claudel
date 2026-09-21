using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySqlConnector;
using Claudel.Data;
using Claudel.Models;

namespace Claudel.Repositories;

public class DocumentRepository
{
    private readonly Database _database;

    public DocumentRepository(Database database)
    {
        _database = database;
    }

    public async Task<int> CreateAsync(
        Document document)
    {
        await using var connection =
            await _database.OpenConnectionAsync();

        const string sql = """
            INSERT INTO documents
            (
                title,
                author,
                category,
                tags,
                year,
                s3_key,
                cover_s3_key
            )
            VALUES
            (
                @title,
                @author,
                @category,
                @tags,
                @year,
                @s3_key,
                @cover_s3_key
            );
            """;

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "@title",
            document.Title);

        command.Parameters.AddWithValue(
            "@author",
            document.Author);

        command.Parameters.AddWithValue(
            "@category",
            document.Category);

        command.Parameters.AddWithValue(
            "@tags",
            document.Tags);

        command.Parameters.AddWithValue(
            "@year",
            document.Year.HasValue
                ? document.Year.Value
                : DBNull.Value);

        command.Parameters.AddWithValue(
            "@s3_key",
            string.IsNullOrWhiteSpace(document.S3Key)
                ? DBNull.Value
                : document.S3Key);

        command.Parameters.AddWithValue(
            "@cover_s3_key",
            string.IsNullOrWhiteSpace(document.CoverS3Key)
                ? DBNull.Value
                : document.CoverS3Key);

        await command.ExecuteNonQueryAsync();

        return checked(
            (int)command.LastInsertedId);
    }

    public async Task<List<Document>> SearchAsync(
        string keyword)
    {
        await using var connection =
            await _database.OpenConnectionAsync();

        const string sql = """
            SELECT
                id,
                title,
                author,
                category,
                tags,
                year,
                s3_key,
                cover_s3_key,
                created_at,
                updated_at
            FROM documents
            WHERE
                @keyword = ''
                OR title LIKE @pattern
                OR author LIKE @pattern
                OR category LIKE @pattern
                OR tags LIKE @pattern
            ORDER BY title;
            """;

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "@keyword",
            keyword);

        command.Parameters.AddWithValue(
            "@pattern",
            $"%{keyword}%");

        var documents =
            new List<Document>();

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            documents.Add(
                ReadDocument(reader));
        }

        return documents;
    }

    public async Task<Document?> GetByIdAsync(
        int id)
    {
        await using var connection =
            await _database.OpenConnectionAsync();

        const string sql = """
            SELECT
                id,
                title,
                author,
                category,
                tags,
                year,
                s3_key,
                cover_s3_key,
                created_at,
                updated_at
            FROM documents
            WHERE id = @id;
            """;

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "@id",
            id);

        await using var reader =
            await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return ReadDocument(reader);
    }

    public async Task<bool> UpdateAsync(
        Document document)
    {
        if (document.Id <= 0)
        {
            throw new ArgumentException(
                "Document ID is invalid.",
                nameof(document));
        }

        await using var connection =
            await _database.OpenConnectionAsync();

        const string sql = """
            UPDATE documents
            SET
                title = @title,
                author = @author,
                category = @category,
                tags = @tags,
                year = @year,
                s3_key = @s3_key,
                cover_s3_key = @cover_s3_key
            WHERE id = @id;
            """;

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "@id",
            document.Id);

        command.Parameters.AddWithValue(
            "@title",
            document.Title);

        command.Parameters.AddWithValue(
            "@author",
            document.Author);

        command.Parameters.AddWithValue(
            "@category",
            document.Category);

        command.Parameters.AddWithValue(
            "@tags",
            document.Tags);

        command.Parameters.AddWithValue(
            "@year",
            document.Year.HasValue
                ? document.Year.Value
                : DBNull.Value);

        command.Parameters.AddWithValue(
            "@s3_key",
            string.IsNullOrWhiteSpace(document.S3Key)
                ? DBNull.Value
                : document.S3Key);

        command.Parameters.AddWithValue(
            "@cover_s3_key",
            string.IsNullOrWhiteSpace(document.CoverS3Key)
                ? DBNull.Value
                : document.CoverS3Key);

        var affectedRows =
            await command.ExecuteNonQueryAsync();

        return affectedRows > 0;
    }

    public async Task<bool> DeleteAsync(
        int id)
    {
        if (id <= 0)
        {
            throw new ArgumentException(
                "Document ID is invalid.",
                nameof(id));
        }

        await using var connection =
            await _database.OpenConnectionAsync();

        const string sql = """
            DELETE FROM documents
            WHERE id = @id;
            """;

        await using var command =
            new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "@id",
            id);

        var affectedRows =
            await command.ExecuteNonQueryAsync();

        return affectedRows > 0;
    }

    private static Document ReadDocument(
        MySqlDataReader reader)
    {
        return new Document
        {
            Id =
                reader.GetInt32("id"),

            Title =
                reader.GetString("title"),

            Author =
                reader.IsDBNull(
                    reader.GetOrdinal("author"))
                    ? ""
                    : reader.GetString("author"),

            Category =
                reader.IsDBNull(
                    reader.GetOrdinal("category"))
                    ? ""
                    : reader.GetString("category"),

            Tags =
                reader.IsDBNull(
                    reader.GetOrdinal("tags"))
                    ? ""
                    : reader.GetString("tags"),

            Year =
                reader.IsDBNull(
                    reader.GetOrdinal("year"))
                    ? null
                    : reader.GetInt32("year"),

            S3Key =
                reader.IsDBNull(
                    reader.GetOrdinal("s3_key"))
                    ? ""
                    : reader.GetString("s3_key"),

            CoverS3Key =
                reader.IsDBNull(
                    reader.GetOrdinal("cover_s3_key"))
                    ? ""
                    : reader.GetString("cover_s3_key"),

            CreatedAt =
                reader.GetDateTime("created_at"),

            UpdatedAt =
                reader.GetDateTime("updated_at")
        };
    }
}
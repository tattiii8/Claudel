using System;
using System.Collections.Generic;
using System.Linq;
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

        await using var transaction =
            await connection.BeginTransactionAsync();

        try
        {
            const string sql = """
                INSERT INTO documents
                (
                    title,
                    category,
                    publication_date,
                    s3_bucket,
                    s3_key,
                    cover_s3_key
                )
                VALUES
                (
                    @title,
                    @category,
                    @publication_date,
                    @s3_bucket,
                    @s3_key,
                    @cover_s3_key
                );
                """;

            await using var command =
                new MySqlCommand(
                    sql,
                    connection,
                    transaction);

            command.Parameters.AddWithValue(
                "@title",
                document.Title);

            command.Parameters.AddWithValue(
                "@category",
                string.IsNullOrWhiteSpace(document.Category)
                    ? DBNull.Value
                    : document.Category);

            command.Parameters.AddWithValue(
                "@publication_date",
                document.PublicationDate.HasValue
                    ? document.PublicationDate.Value.Date
                    : DBNull.Value);

            command.Parameters.AddWithValue(
                "@s3_bucket",
                string.IsNullOrWhiteSpace(document.S3Bucket)
                    ? DBNull.Value
                    : document.S3Bucket);

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

            var documentId =
                checked((int)command.LastInsertedId);

            await SaveAuthorsAsync(
                connection,
                transaction,
                documentId,
                document.Authors);

            await SaveTagsAsync(
                connection,
                transaction,
                documentId,
                document.Tags);

            await transaction.CommitAsync();

            return documentId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<Document>> SearchAsync(
        string keyword)
    {
        await using var connection =
            await _database.OpenConnectionAsync();

        const string sql = """
            SELECT
                d.id,
                d.title,
                d.category,
                d.publication_date,
                d.s3_bucket,
                d.s3_key,
                d.cover_s3_key,
                d.redmine_issue_id,
                d.redmine_issue_url,
                d.created_at,
                d.updated_at
            FROM documents d
            WHERE
                @keyword = ''
                OR d.title LIKE @pattern
                OR d.category LIKE @pattern
                OR EXISTS
                (
                    SELECT 1
                    FROM document_authors da
                    INNER JOIN authors a
                        ON a.id = da.author_id
                    WHERE
                        da.document_id = d.id
                        AND a.name LIKE @pattern
                )
                OR EXISTS
                (
                    SELECT 1
                    FROM document_tags dt
                    INNER JOIN tags t
                        ON t.id = dt.tag_id
                    WHERE
                        dt.document_id = d.id
                        AND t.name LIKE @pattern
                )
            ORDER BY d.title;
            """;

        var documents =
            new List<Document>();

        /*
         * Readerをこのスコープ内で完全に閉じる。
         * Readerが開いたままLoadRelationsAsyncを呼ぶと、
         * 同じMySqlConnectionを使用できない。
         */
        await using (
            var command =
                new MySqlCommand(
                    sql,
                    connection))
        {
            command.Parameters.AddWithValue(
                "@keyword",
                keyword);

            command.Parameters.AddWithValue(
                "@pattern",
                $"%{keyword}%");

            await using var reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                documents.Add(
                    ReadDocument(reader));
            }
        }

        /*
         * ここではSELECTのReaderが既に閉じているため、
         * 同じConnectionを使って関連データを取得できる。
         */
        foreach (var document in documents)
        {
            await LoadRelationsAsync(
                connection,
                document);
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
                d.id,
                d.title,
                d.category,
                d.publication_date,
                d.s3_bucket,
                d.s3_key,
                d.cover_s3_key,
                d.redmine_issue_id,
                d.redmine_issue_url,
                d.created_at,
                d.updated_at
            FROM documents d
            WHERE d.id = @id;
            """;

        Document? document;

        /*
         * Readerをusingブロック内で完全に閉じてから
         * LoadRelationsAsyncを呼び出す。
         */
        await using (
            var command =
                new MySqlCommand(
                    sql,
                    connection))
        {
            command.Parameters.AddWithValue(
                "@id",
                id);

            await using var reader =
                await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return null;
            }

            document =
                ReadDocument(reader);
        }

        await LoadRelationsAsync(
            connection,
            document);

        return document;
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

        await using var transaction =
            await connection.BeginTransactionAsync();

        try
        {
            const string sql = """
                UPDATE documents
                SET
                    title = @title,
                    category = @category,
                    publication_date = @publication_date,
                    s3_bucket = @s3_bucket,
                    s3_key = @s3_key,
                    cover_s3_key = @cover_s3_key,
                    redmine_issue_id = @redmine_issue_id,
                    redmine_issue_url = @redmine_issue_url
                WHERE id = @id;
                """;

            await using var command =
                new MySqlCommand(
                    sql,
                    connection,
                    transaction);

            command.Parameters.AddWithValue(
                "@id",
                document.Id);

            command.Parameters.AddWithValue(
                "@title",
                document.Title);

            command.Parameters.AddWithValue(
                "@category",
                string.IsNullOrWhiteSpace(document.Category)
                    ? DBNull.Value
                    : document.Category);

            command.Parameters.AddWithValue(
                "@publication_date",
                document.PublicationDate.HasValue
                    ? document.PublicationDate.Value.Date
                    : DBNull.Value);

            command.Parameters.AddWithValue(
                "@s3_bucket",
                string.IsNullOrWhiteSpace(document.S3Bucket)
                    ? DBNull.Value
                    : document.S3Bucket);

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

            command.Parameters.AddWithValue(
                "@redmine_issue_id",
                document.RedmineIssueId.HasValue
                    ? document.RedmineIssueId.Value
                    : DBNull.Value);

            command.Parameters.AddWithValue(
                "@redmine_issue_url",
                string.IsNullOrWhiteSpace(document.RedmineIssueUrl)
                    ? DBNull.Value
                    : document.RedmineIssueUrl);

            var affectedRows =
                await command.ExecuteNonQueryAsync();

            if (affectedRows == 0)
            {
                await transaction.RollbackAsync();
                return false;
            }

            await DeleteAuthorsAsync(
                connection,
                transaction,
                document.Id);

            await DeleteTagsAsync(
                connection,
                transaction,
                document.Id);

            await SaveAuthorsAsync(
                connection,
                transaction,
                document.Id,
                document.Authors);

            await SaveTagsAsync(
                connection,
                transaction,
                document.Id,
                document.Tags);

            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
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
            new MySqlCommand(
                sql,
                connection);

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

            Category =
                reader.IsDBNull(
                    reader.GetOrdinal("category"))
                    ? ""
                    : reader.GetString("category"),

            PublicationDate =
                reader.IsDBNull(
                    reader.GetOrdinal("publication_date"))
                    ? null
                    : reader.GetDateTime(
                        reader.GetOrdinal("publication_date")),

            S3Bucket =
                reader.IsDBNull(
                    reader.GetOrdinal("s3_bucket"))
                    ? ""
                    : reader.GetString("s3_bucket"),

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

            RedmineIssueId =
                reader.IsDBNull(
                    reader.GetOrdinal("redmine_issue_id"))
                    ? null
                    : reader.GetInt32(
                        reader.GetOrdinal("redmine_issue_id")),

            RedmineIssueUrl =
                reader.IsDBNull(
                    reader.GetOrdinal("redmine_issue_url"))
                    ? ""
                    : reader.GetString("redmine_issue_url"),

            CreatedAt =
                reader.GetDateTime("created_at"),

            UpdatedAt =
                reader.GetDateTime("updated_at")
        };
    }

    private static async Task LoadRelationsAsync(
        MySqlConnection connection,
        Document document)
    {
        /*
         * Authors
         */

        const string authorSql = """
            SELECT
                a.id,
                a.name,
                da.author_order
            FROM document_authors da
            INNER JOIN authors a
                ON a.id = da.author_id
            WHERE da.document_id = @document_id
            ORDER BY da.author_order;
            """;

        await using (
            var authorCommand =
                new MySqlCommand(
                    authorSql,
                    connection))
        {
            authorCommand.Parameters.AddWithValue(
                "@document_id",
                document.Id);

            await using var authorReader =
                await authorCommand.ExecuteReaderAsync();

            while (await authorReader.ReadAsync())
            {
                document.Authors.Add(
                    new Author
                    {
                        Id =
                            authorReader.GetInt64("id"),

                        Name =
                            authorReader.GetString("name"),

                        Order =
                            authorReader.GetInt32("author_order")
                    });
            }
        }

        /*
         * Tags
         *
         * Author readerを完全に閉じてから
         * Tag readerを開く。
         */

        const string tagSql = """
            SELECT
                t.id,
                t.name
            FROM document_tags dt
            INNER JOIN tags t
                ON t.id = dt.tag_id
            WHERE dt.document_id = @document_id
            ORDER BY t.name;
            """;

        await using (
            var tagCommand =
                new MySqlCommand(
                    tagSql,
                    connection))
        {
            tagCommand.Parameters.AddWithValue(
                "@document_id",
                document.Id);

            await using var tagReader =
                await tagCommand.ExecuteReaderAsync();

            while (await tagReader.ReadAsync())
            {
                document.Tags.Add(
                    new Tag
                    {
                        Id =
                            tagReader.GetInt64("id"),

                        Name =
                            tagReader.GetString("name")
                    });
            }
        }
    }

    private static async Task SaveAuthorsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int documentId,
        IEnumerable<Author> authors)
    {
        const string findSql = """
            SELECT id
            FROM authors
            WHERE name = @name;
            """;

        const string insertSql = """
            INSERT INTO authors
            (
                name
            )
            VALUES
            (
                @name
            );
            """;

        const string relationSql = """
            INSERT INTO document_authors
            (
                document_id,
                author_id,
                author_order
            )
            VALUES
            (
                @document_id,
                @author_id,
                @author_order
            );
            """;

        foreach (var author in authors)
        {
            var name =
                author.Name?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            long authorId;

            await using (
                var findCommand =
                    new MySqlCommand(
                        findSql,
                        connection,
                        transaction))
            {
                findCommand.Parameters.AddWithValue(
                    "@name",
                    name);

                var result =
                    await findCommand.ExecuteScalarAsync();

                if (result != null)
                {
                    authorId =
                        Convert.ToInt64(result);
                }
                else
                {
                    await using var insertCommand =
                        new MySqlCommand(
                            insertSql,
                            connection,
                            transaction);

                    insertCommand.Parameters.AddWithValue(
                        "@name",
                        name);

                    await insertCommand.ExecuteNonQueryAsync();

                    authorId =
                        insertCommand.LastInsertedId;
                }
            }

            await using var relationCommand =
                new MySqlCommand(
                    relationSql,
                    connection,
                    transaction);

            relationCommand.Parameters.AddWithValue(
                "@document_id",
                documentId);

            relationCommand.Parameters.AddWithValue(
                "@author_id",
                authorId);

            relationCommand.Parameters.AddWithValue(
                "@author_order",
                author.Order);

            await relationCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task SaveTagsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int documentId,
        IEnumerable<Tag> tags)
    {
        const string findSql = """
            SELECT id
            FROM tags
            WHERE name = @name;
            """;

        const string insertSql = """
            INSERT INTO tags
            (
                name
            )
            VALUES
            (
                @name
            );
            """;

        const string relationSql = """
            INSERT INTO document_tags
            (
                document_id,
                tag_id
            )
            VALUES
            (
                @document_id,
                @tag_id
            );
            """;

        var addedTagIds =
            new HashSet<long>();

        foreach (var tag in tags)
        {
            var name =
                tag.Name?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            long tagId;

            await using (
                var findCommand =
                    new MySqlCommand(
                        findSql,
                        connection,
                        transaction))
            {
                findCommand.Parameters.AddWithValue(
                    "@name",
                    name);

                var result =
                    await findCommand.ExecuteScalarAsync();

                if (result != null)
                {
                    tagId =
                        Convert.ToInt64(result);
                }
                else
                {
                    await using var insertCommand =
                        new MySqlCommand(
                            insertSql,
                            connection,
                            transaction);

                    insertCommand.Parameters.AddWithValue(
                        "@name",
                        name);

                    await insertCommand.ExecuteNonQueryAsync();

                    tagId =
                        insertCommand.LastInsertedId;
                }
            }

            if (!addedTagIds.Add(tagId))
            {
                continue;
            }

            await using var relationCommand =
                new MySqlCommand(
                    relationSql,
                    connection,
                    transaction);

            relationCommand.Parameters.AddWithValue(
                "@document_id",
                documentId);

            relationCommand.Parameters.AddWithValue(
                "@tag_id",
                tagId);

            await relationCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task DeleteAuthorsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int documentId)
    {
        const string sql = """
            DELETE FROM document_authors
            WHERE document_id = @document_id;
            """;

        await using var command =
            new MySqlCommand(
                sql,
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "@document_id",
            documentId);

        await command.ExecuteNonQueryAsync();
    }

    private static async Task DeleteTagsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        int documentId)
    {
        const string sql = """
            DELETE FROM document_tags
            WHERE document_id = @document_id;
            """;

        await using var command =
            new MySqlCommand(
                sql,
                connection,
                transaction);

        command.Parameters.AddWithValue(
            "@document_id",
            documentId);

        await command.ExecuteNonQueryAsync();
    }
}
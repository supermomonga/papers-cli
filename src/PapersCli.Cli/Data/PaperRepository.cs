using System.Text;
using Microsoft.Data.Sqlite;
using PapersCli.Cli.Config;
using PapersCli.Cli.Models;

namespace PapersCli.Cli.Data;

public class PaperRepository
{
    private readonly string _connectionString;

    public PaperRepository(AppConfig config)
        : this($"Data Source={AppConfig.DatabasePath}")
    {
        Directory.CreateDirectory(AppConfig.DataDir);
    }

    public PaperRepository(string connectionString)
    {
        _connectionString = connectionString;
        InitializeDatabase();
    }

    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private void InitializeDatabase()
    {
        using var connection = CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS papers (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                source TEXT NOT NULL,
                source_id TEXT NOT NULL,
                title TEXT NOT NULL,
                authors TEXT NOT NULL,
                published_at TEXT,
                abstract TEXT,
                url TEXT NOT NULL,
                doi TEXT,
                journal TEXT,
                categories TEXT,
                created_at TEXT NOT NULL,
                UNIQUE(source, source_id)
            );

            CREATE TABLE IF NOT EXISTS paper_files (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                paper_id INTEGER NOT NULL REFERENCES papers(id) ON DELETE CASCADE,
                format TEXT NOT NULL,
                file_path TEXT NOT NULL,
                source_url TEXT NOT NULL,
                downloaded_at TEXT NOT NULL,
                UNIQUE(paper_id, format)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public async Task<Paper?> GetPaperAsync(string source, string sourceId)
    {
        using var connection = CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM papers WHERE source = @source AND source_id = @sourceId";
        cmd.Parameters.AddWithValue("@source", source);
        cmd.Parameters.AddWithValue("@sourceId", sourceId);
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadPaper(reader) : null;
    }

    public async Task<IReadOnlyList<Paper>> SearchPapersAsync(
        string? query = null,
        string? source = null,
        string? author = null,
        int? fromYear = null,
        int? toYear = null,
        string? category = null,
        string sort = "date",
        int limit = 20)
    {
        using var connection = CreateConnection();
        using var cmd = connection.CreateCommand();

        var sb = new StringBuilder("SELECT * FROM papers WHERE 1=1");

        if (!string.IsNullOrEmpty(query))
        {
            sb.Append(" AND (title LIKE @query OR abstract LIKE @query OR authors LIKE @query)");
            cmd.Parameters.AddWithValue("@query", $"%{query}%");
        }
        if (!string.IsNullOrEmpty(source))
        {
            sb.Append(" AND source = @source");
            cmd.Parameters.AddWithValue("@source", source);
        }
        if (!string.IsNullOrEmpty(author))
        {
            sb.Append(" AND authors LIKE @author");
            cmd.Parameters.AddWithValue("@author", $"%{author}%");
        }
        if (fromYear.HasValue)
        {
            sb.Append(" AND published_at >= @fromDate");
            cmd.Parameters.AddWithValue("@fromDate", $"{fromYear.Value}-01-01");
        }
        if (toYear.HasValue)
        {
            sb.Append(" AND published_at <= @toDate");
            cmd.Parameters.AddWithValue("@toDate", $"{toYear.Value}-12-31");
        }
        if (!string.IsNullOrEmpty(category))
        {
            sb.Append(" AND categories LIKE @category");
            cmd.Parameters.AddWithValue("@category", $"%{category}%");
        }

        sb.Append(sort switch
        {
            "title" => " ORDER BY title ASC",
            "author" => " ORDER BY authors ASC",
            "downloaded_at" => " ORDER BY created_at DESC",
            _ => " ORDER BY published_at DESC",
        });

        sb.Append(" LIMIT @limit");
        cmd.Parameters.AddWithValue("@limit", limit);

        cmd.CommandText = sb.ToString();

        var results = new List<Paper>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(ReadPaper(reader));
        return results;
    }

    public async Task<long> InsertPaperAsync(Paper paper)
    {
        using var connection = CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO papers (source, source_id, title, authors, published_at, abstract, url, doi, journal, categories, created_at)
            VALUES (@Source, @SourceId, @Title, @Authors, @PublishedAt, @Abstract, @Url, @Doi, @Journal, @Categories, @CreatedAt);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@Source", paper.Source);
        cmd.Parameters.AddWithValue("@SourceId", paper.SourceId);
        cmd.Parameters.AddWithValue("@Title", paper.Title);
        cmd.Parameters.AddWithValue("@Authors", paper.Authors);
        cmd.Parameters.AddWithValue("@PublishedAt", (object?)paper.PublishedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Abstract", (object?)paper.Abstract ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Url", paper.Url);
        cmd.Parameters.AddWithValue("@Doi", (object?)paper.Doi ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Journal", (object?)paper.Journal ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Categories", (object?)paper.Categories ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", paper.CreatedAt);

        var result = await cmd.ExecuteScalarAsync();
        return (long)result!;
    }

    public async Task InsertPaperFileAsync(PaperFile file)
    {
        using var connection = CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO paper_files (paper_id, format, file_path, source_url, downloaded_at)
            VALUES (@PaperId, @Format, @FilePath, @SourceUrl, @DownloadedAt);
            """;
        cmd.Parameters.AddWithValue("@PaperId", file.PaperId);
        cmd.Parameters.AddWithValue("@Format", file.Format);
        cmd.Parameters.AddWithValue("@FilePath", file.FilePath);
        cmd.Parameters.AddWithValue("@SourceUrl", file.SourceUrl);
        cmd.Parameters.AddWithValue("@DownloadedAt", file.DownloadedAt);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<PaperFile>> GetPaperFilesAsync(long paperId)
    {
        using var connection = CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM paper_files WHERE paper_id = @paperId";
        cmd.Parameters.AddWithValue("@paperId", paperId);

        var results = new List<PaperFile>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(ReadPaperFile(reader));
        return results;
    }

    public async Task<IReadOnlyList<PaperFile>> GetAllPaperFilesForSourceAsync(string source, string sourceId)
    {
        using var connection = CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT pf.* FROM paper_files pf
            INNER JOIN papers p ON p.id = pf.paper_id
            WHERE p.source = @source AND p.source_id = @sourceId
            """;
        cmd.Parameters.AddWithValue("@source", source);
        cmd.Parameters.AddWithValue("@sourceId", sourceId);

        var results = new List<PaperFile>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(ReadPaperFile(reader));
        return results;
    }

    public async Task DeletePaperAsync(long paperId)
    {
        using var connection = CreateConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM papers WHERE id = @paperId";
        cmd.Parameters.AddWithValue("@paperId", paperId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static Paper ReadPaper(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(reader.GetOrdinal("id")),
        Source = reader.GetString(reader.GetOrdinal("source")),
        SourceId = reader.GetString(reader.GetOrdinal("source_id")),
        Title = reader.GetString(reader.GetOrdinal("title")),
        Authors = reader.GetString(reader.GetOrdinal("authors")),
        PublishedAt = reader.IsDBNull(reader.GetOrdinal("published_at")) ? null : reader.GetString(reader.GetOrdinal("published_at")),
        Abstract = reader.IsDBNull(reader.GetOrdinal("abstract")) ? null : reader.GetString(reader.GetOrdinal("abstract")),
        Url = reader.GetString(reader.GetOrdinal("url")),
        Doi = reader.IsDBNull(reader.GetOrdinal("doi")) ? null : reader.GetString(reader.GetOrdinal("doi")),
        Journal = reader.IsDBNull(reader.GetOrdinal("journal")) ? null : reader.GetString(reader.GetOrdinal("journal")),
        Categories = reader.IsDBNull(reader.GetOrdinal("categories")) ? null : reader.GetString(reader.GetOrdinal("categories")),
        CreatedAt = reader.GetString(reader.GetOrdinal("created_at")),
    };

    private static PaperFile ReadPaperFile(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(reader.GetOrdinal("id")),
        PaperId = reader.GetInt64(reader.GetOrdinal("paper_id")),
        Format = reader.GetString(reader.GetOrdinal("format")),
        FilePath = reader.GetString(reader.GetOrdinal("file_path")),
        SourceUrl = reader.GetString(reader.GetOrdinal("source_url")),
        DownloadedAt = reader.GetString(reader.GetOrdinal("downloaded_at")),
    };
}

using System.Data.SQLite;
using Dapper;

namespace DotNetFlix.Data;

public class FileUpload
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public string Name { get; set; }
    public long Size { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? UploadCompletedUtc { get; set; }
}

public class FileUploadsTable
{
    public const string TableName = "FileUploads";
    public long Id { get; set; }
    public long SessionId { get; set; }
    public string Name { get; set; }
    public long Size { get; set; }
    public long CreatedUnixTimestamp { get; set; }    
    public long? UploadCompletedUnixTimestamp { get; set; }
}

public static class FileDataExtensions
{
    public static FileUpload GetFileUpload(this SQLiteConnection sql, long fileUploadId)
    {
        var row = sql.QueryFirst<FileUploadsTable>($@"SELECT * FROM {FileUploadsTable.TableName} WHERE [{nameof(FileUploadsTable.Id)}] = @{nameof(FileUploadsTable.Id)}", new
        {
            Id = fileUploadId
        });

        return Map(row);
    }

    public static FileUpload CreateFileUpload(this SQLiteConnection sql, long sessionId, string fileName, long fileSize)
    {
        var file = new FileUpload()
        {
            CreatedUtc = DateTime.UtcNow,
            Name = fileName,
            SessionId = sessionId,
            Size = fileSize,
        };

        file.Id = sql.ExecuteScalar<long>($@"
            INSERT INTO {FileUploadsTable.TableName}
            (
                [{nameof(FileUploadsTable.SessionId)}],
                [{nameof(FileUploadsTable.Name)}],
                [{nameof(FileUploadsTable.Size)}],
                [{nameof(FileUploadsTable.CreatedUnixTimestamp)}]
            )
            VALUES
            (
                @{nameof(FileUploadsTable.SessionId)},
                @{nameof(FileUploadsTable.Name)},
                @{nameof(FileUploadsTable.Size)},
                @{nameof(FileUploadsTable.CreatedUnixTimestamp)}
            )
            RETURNING [{nameof(FileUploadsTable.Id)}]", Map(file));

        return file;
    }


    public static void SetFileUploadCompleted(this SQLiteConnection sql, long fileId)
    {
        sql.Execute($@"UPDATE {FileUploadsTable.TableName}
                        SET     [{nameof(FileUploadsTable.UploadCompletedUnixTimestamp)}] = @{nameof(FileUploadsTable.UploadCompletedUnixTimestamp)}
                        WHERE   [{nameof(FileUploadsTable.Id)}] = @{nameof(FileUploadsTable.Id)}", new
        {
            Id = fileId,
            UploadCompletedUnixTimestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds()
        });
    }

    public static FileUploadsTable Map(FileUpload file) => new FileUploadsTable
    {
        Id = file.Id,
        SessionId= file.SessionId,
        Name = file.Name,
        Size = file.Size,
        CreatedUnixTimestamp = new DateTimeOffset(file.CreatedUtc).ToUnixTimeSeconds(),
        UploadCompletedUnixTimestamp = file.UploadCompletedUtc.HasValue ? new DateTimeOffset(file.UploadCompletedUtc.Value).ToUnixTimeSeconds() : null,
    };

    public static FileUpload Map(FileUploadsTable file) => new FileUpload
    {
        Id = file.Id,
        SessionId = file.SessionId,
        Name = file.Name,
        Size = file.Size,
        CreatedUtc = DateTimeOffset.FromUnixTimeSeconds(file.CreatedUnixTimestamp).UtcDateTime,
        UploadCompletedUtc = file.UploadCompletedUnixTimestamp.HasValue ? DateTimeOffset.FromUnixTimeSeconds(file.UploadCompletedUnixTimestamp.Value).UtcDateTime : null
    };
}
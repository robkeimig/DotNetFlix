using System.Data.SQLite;
using Dapper;

namespace DotNetFlix.Data;

public class Media
{
    public long Id { get; set; }
    public string Title { get; set; }
    public long Size { get; set; }
    public bool IsPending { get; set; }
}

public class MediaTable
{
    public const string TableName = "Media";
    public long Id { get; set; }
    public string Title { get; set; }
    public long Size { get; set; }
    public bool IsPending { get; set; }
}

public static class MediaDataExtensions
{
    public static List<Media> GetMedia(this SQLiteConnection sql)
    {
        var rows = sql.Query<MediaTable>($@"
            SELECT [{nameof(MediaTable.Id)}],
                    [{nameof(MediaTable.Title)}]
            FROM {MediaTable.TableName}");

        return Map(rows);
    }

    public static Media GetMedia(this SQLiteConnection sql, long mediaId)
    {
        var row = sql.QueryFirst<MediaTable>($@"
            SELECT * 
            FROM {MediaTable.TableName}
            WHERE   [{nameof(MediaTable.Id)}] = @{nameof(MediaTable.Id)}", new
        {
            Id = mediaId
        });

        return Map(row);
    }

    public static async Task<Media> CreateMedia(this SQLiteConnection sql, string uploadedFile, string title)
    {
        var configuration = sql.GetConfiguration();
        var transcodedFile = Guid.NewGuid().ToString("N")+".mp4";
        var transcodingStatus = MediaExtensions.TranscodeToH264(uploadedFile, transcodedFile);

        while (!transcodingStatus.Complete) { Thread.Sleep(1); } //TODO: Iterate on this.

        var transcodedFileInfo = new FileInfo(transcodedFile);

        var media = new Media
        {
            Title = title,
            IsPending = true,
            Size = transcodedFileInfo.Length
        };

        media.Id = sql.ExecuteScalar<long>(@$"
            INSERT INTO [{MediaTable.TableName}]
            (
                [{nameof(MediaTable.Title)}],
                [{nameof(MediaTable.IsPending)}],
                [{nameof(MediaTable.Size)}]
            )
            VALUES 
            (
                @{nameof(MediaTable.Title)},
                @{nameof(MediaTable.IsPending)},
                @{nameof(MediaTable.Size)}
            )
            RETURNING [{nameof(MediaTable.Id)}]", Map(media));

        var mediaBlockSequence = 0;
        var transcodedFileStream = new FileStream(transcodedFile, FileMode.Open, FileAccess.Read);

        while (await sql.CreateMediaBlock(media.Id, mediaBlockSequence++, transcodedFileStream));

        transcodedFileStream.Close();
        await transcodedFileStream.DisposeAsync();

        File.Delete(uploadedFile);
        File.Delete(transcodedFile);

        sql.Execute($@"UPDATE {MediaTable.TableName} 
            SET [{nameof(MediaTable.IsPending)}] = 0
            WHERE   [{nameof(MediaTable.Id)}] = @{nameof(MediaTable.Id)}", new
        {
            Id = media.Id
        });

        return media;
    }

    public static MediaTable Map(Media media) => new MediaTable
    {
        Id = media.Id,
        IsPending = media.IsPending,
        Title = media.Title,
        Size = media.Size
    };

    public static Media Map(MediaTable media) => new Media
    {
        Id = media.Id,
        IsPending = media.IsPending,
        Title = media.Title,
        Size = media.Size
    };

    public static List<Media> Map(IEnumerable<MediaTable> media) => media.Select(Map).ToList();
}
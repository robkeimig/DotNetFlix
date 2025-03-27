using System.Data.SQLite;
using Dapper;

namespace DotNetFlix.Data;

public class Media
{
    public long Id { get; set; }
    public string Title { get; set; }
    public long Size { get; set; }
    public bool IsPending { get; set; }
    public string PendingStatus { get; set; }
}

public class MediaTable
{
    public const string TableName = "Media";
    public long Id { get; set; }
    public string Title { get; set; }
    public long Size { get; set; }
    public bool IsPending { get; set; }
    public string PendingStatus { get; set; }
}

public static class MediaDataExtensions
{
    public static List<Media> GetMedia(this SQLiteConnection sql)
    {
        var rows = sql.Query<MediaTable>($@"
            SELECT *
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

    public static List<Media> GetPendingMedia(this SQLiteConnection sql)
    {
        var rows = sql.Query<MediaTable>($@"
            SELECT  *
            FROM    {MediaTable.TableName}
            WHERE   [{nameof(MediaTable.IsPending)}] = 1");

        return Map(rows);
    }

    public static void SetMediaPendingStatus(this SQLiteConnection sql, long mediaId, string status)
    {
        sql.Execute($@"
            UPDATE  {MediaTable.TableName}
            SET     [{nameof(MediaTable.PendingStatus)}] = @{nameof(MediaTable.PendingStatus)}
            WHERE   [{nameof(MediaTable.Id)}] = @{nameof(MediaTable.Id)}", new
        {
            Id = mediaId,
            PendingStatus = status
        });
    }

    public static Media CreateMedia(this SQLiteConnection sql, string uploadedFile, string title)
    {
        var media = new Media
        {
            Title = title,
            IsPending = true,
            PendingStatus = "Transcoding",
        };

        media.Id = sql.ExecuteScalar<long>(@$"
            INSERT INTO [{MediaTable.TableName}]
            (
                [{nameof(MediaTable.Title)}],
                [{nameof(MediaTable.IsPending)}],
                [{nameof(MediaTable.PendingStatus)}]
            )
            VALUES 
            (
                @{nameof(MediaTable.Title)},
                @{nameof(MediaTable.IsPending)},
                @{nameof(MediaTable.PendingStatus)}
            )
            RETURNING [{nameof(MediaTable.Id)}]", Map(media));

        Task.Run(async () =>
        {
            var configuration = sql.GetConfiguration();
            var transcodedFile = Guid.NewGuid().ToString("N") + ".mp4";
            MediaExtensions.TranscodeToH264(sql, media.Id, uploadedFile, transcodedFile);
            var transcodedFileInfo = new FileInfo(transcodedFile);
            media.Size = transcodedFileInfo.Length;

            sql.Execute($@"
                UPDATE  {MediaTable.TableName} 
                SET     [{nameof(MediaTable.Size)}] = @{nameof(MediaTable.Size)}
                WHERE   [{nameof(MediaTable.Id)}] = @{nameof(MediaTable.Id)}", new
            {
                Id = media.Id,
                Size = media.Size
            });

            var mediaBlockSequence = 0;
            var totalBlocks = (media.Size + Constants.MediaBlockSize - 1) / Constants.MediaBlockSize;
            var transcodedFileStream = new FileStream(transcodedFile, FileMode.Open, FileAccess.Read);

            do
            {
                var percent = (int)(1f * mediaBlockSequence / totalBlocks * 100f);
                sql.SetMediaPendingStatus(media.Id, $"Uploading - Block {mediaBlockSequence} / {totalBlocks} ({percent}%)");
            }
            while (await sql.CreateMediaBlock(media.Id, mediaBlockSequence++, transcodedFileStream));

            transcodedFileStream.Close();
            await transcodedFileStream.DisposeAsync();
            
            File.Delete(uploadedFile);
            File.Delete(transcodedFile);

            sql.Execute($@"
                UPDATE  {MediaTable.TableName} 
                SET     [{nameof(MediaTable.IsPending)}] = 0,
                        [{nameof(MediaTable.PendingStatus)}] = NULL
                WHERE   [{nameof(MediaTable.Id)}] = @{nameof(MediaTable.Id)}", new
            {
                Id = media.Id,
            });
        });

        return media;
    }

    public static MediaTable Map(Media media) => new MediaTable
    {
        Id = media.Id,
        IsPending = media.IsPending,
        PendingStatus = media.PendingStatus,
        Title = media.Title,
        Size = media.Size
    };

    public static Media Map(MediaTable media) => new Media
    {
        Id = media.Id,
        IsPending = media.IsPending,
        PendingStatus  = media.PendingStatus,
        Title = media.Title,
        Size = media.Size
    };

    public static List<Media> Map(IEnumerable<MediaTable> media) => media.Select(Map).ToList();
}
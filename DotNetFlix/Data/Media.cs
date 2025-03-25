using System.Data.SQLite;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Transfer;
using Dapper;

namespace DotNetFlix.Data;

public class Media
{
    public long Id { get; set; }
    public string Title { get; set; }
    public bool IsPending { get; set; }
}

public class MediaTable
{
    public const string TableName = "Media";
    public long Id { get; set; }
    public string Title { get; set; }
    public bool IsPending { get; set; }
}

public static class MediaDataExtensions
{
    

    //public static void WarmupMedia(this SQLiteConnection sql, long mediaId)
    //{
    //    Media media = default;
        
    //    lock (WarmupSyncRoot)
    //    {
    //        media = sql.GetMedia(mediaId);

    //        if (media.IsPending)
    //        {
    //            return;
    //        }

    //        var contentFile = Path.Combine(CachePath, media.ContentId.ToString("N") + ".mp4");
    //        if (File.Exists(contentFile)) { return; }

    //        sql.Execute($@"
    //            UPDATE {MediaTable.TableName}
    //            SET     [{nameof(MediaTable.IsPending)}] = 1
    //            WHERE   [{nameof(MediaTable.Id)}] = @{nameof(MediaTable.Id)}", new
    //        {
    //            Id = mediaId
    //        });
    //    }

    //    Directory.CreateDirectory(CachePath);
    //    Directory.CreateDirectory(CachePreloadPath);

    //    var contentPreloadFileEnc = media.ContentPreloadId.ToString("N") + ".mp4.enc";
    //    var contentPreloadFile = Path.Combine(CachePreloadPath, media.ContentPreloadId.ToString("N") + ".mp4");
    //    DownloadS3File(sql, media.ContentPreloadId, contentPreloadFileEnc);
    //    Cryptography.DecryptFile(contentPreloadFileEnc, contentPreloadFile, media.EncryptionKey);

    //    Task.Run(() =>
    //    {
    //        var contentFileEnc = media.ContentId.ToString("N") + ".mp4.enc";
    //        var contentFile = Path.Combine(CachePath, media.ContentId.ToString("N") + ".mp4");
    //        DownloadS3File(sql, media.ContentId, contentFileEnc);
    //        Cryptography.DecryptFile(contentFileEnc, contentFile, media.EncryptionKey);
    //    });
    //}

    static void DownloadS3File(this SQLiteConnection sql, Guid id, string targetPath)
    {
        var configuration = sql.GetConfiguration();
        var s3ObjectName = id.ToString("N");
        var awsCredentials = new BasicAWSCredentials(configuration.AwsS3AccessKey, configuration.AwsS3SecretKey);
        var s3Client = new AmazonS3Client(awsCredentials, RegionEndpoint.USEast1);
        var fileTransferUtility = new TransferUtility(s3Client);
        fileTransferUtility.Download(targetPath, configuration.AwsS3BucketName, s3ObjectName);
    }

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
        var contentId = Guid.NewGuid();
        var contentFile = contentId.ToString("N")+".mp4";
        var contentStatus = MediaExtensions.TranscodeToH264(uploadedFile, contentFile);
        while (!contentStatus.Complete) { Thread.Sleep(1); } //TODO: Iterate on this.

        var media = new Media
        {
            Title = title,
            IsPending = true,
        };

        media.Id = sql.ExecuteScalar<long>(@$"
            INSERT INTO [{MediaTable.TableName}]
            (
                [{nameof(MediaTable.Title)}],
                [{nameof(MediaTable.IsPending)}]
            )
            VALUES 
            (
                @{nameof(MediaTable.Title)},
                @{nameof(MediaTable.IsPending)} 
            )", Map(media));


        var mediaBlockSequence = 0;
        using var contentFileStream = new FileStream(contentFile, FileMode.Open, FileAccess.Read);
        
        //Create Media Blocks until we've consumed the entire stream.
        while (await sql.CreateMediaBlock(media.Id, mediaBlockSequence++, contentFileStream)) ;

        //Cleanup the original file & content file.
        //The media block creation process will handle any relevant cleanup internally above. 
        File.Delete(uploadedFile);
        File.Delete(contentFile);

        //Flag the Media as available for consumption (IsPending = 0 - all blocks are now available).
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
        Title = media.Title
    };

    public static Media Map(MediaTable media) => new Media
    {
        Id = media.Id,
        IsPending = media.IsPending,
        Title = media.Title
    };

    public static List<Media> Map(IEnumerable<MediaTable> media) => media.Select(Map).ToList();
}
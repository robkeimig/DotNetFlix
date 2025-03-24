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
    public Guid ContentId { get; set; }
    public Guid ContentPreloadId { get; set; }
    public byte[] EncryptionKey { get; set; }
    public bool IsPending { get; set; }
}

public class MediaTable
{
    public const string TableName = "Media";
    public long Id { get; set; }
    public string Title { get; set; }
    public Guid ContentId { get; set; }
    public Guid ContentPreloadId { get; set; }
    public byte[] EncryptionKey { get; set; }
    public bool IsPending { get; set; }
}

public static class MediaDataExtensions
{
    static object WarmupSyncRoot = new object();
    public const string CachePath = "Cache";
    public const string CachePreloadPath = "Cache_Preload";

    public static void WarmupMedia(this SQLiteConnection sql, long mediaId)
    {
        Media media = default;
        
        lock (WarmupSyncRoot)
        {
            media = sql.GetMedia(mediaId);

            if (media.IsPending)
            {
                return;
            }

            var contentFile = Path.Combine(CachePath, media.ContentId.ToString("N") + ".mp4");
            if (File.Exists(contentFile)) { return; }

            sql.Execute($@"
                UPDATE {MediaTable.TableName}
                SET     [{nameof(MediaTable.IsPending)}] = 1
                WHERE   [{nameof(MediaTable.Id)}] = @{nameof(MediaTable.Id)}", new
            {
                Id = mediaId
            });
        }

        Directory.CreateDirectory(CachePath);
        Directory.CreateDirectory(CachePreloadPath);

        var contentPreloadFileEnc = media.ContentPreloadId.ToString("N") + ".mp4.enc";
        var contentPreloadFile = Path.Combine(CachePreloadPath, media.ContentPreloadId.ToString("N") + ".mp4");
        DownloadS3File(sql, media.ContentPreloadId, contentPreloadFileEnc);
        Cryptography.DecryptFile(contentPreloadFileEnc, contentPreloadFile, media.EncryptionKey);

        Task.Run(() =>
        {
            var contentFileEnc = media.ContentId.ToString("N") + ".mp4.enc";
            var contentFile = Path.Combine(CachePath, media.ContentId.ToString("N") + ".mp4");
            DownloadS3File(sql, media.ContentId, contentFileEnc);
            Cryptography.DecryptFile(contentFileEnc, contentFile, media.EncryptionKey);
        });
    }

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

    public static Media CreateMedia(this SQLiteConnection sql, string uploadedFile, string title)
    {
        var configuration = sql.GetConfiguration();
        var awsCredentials = new BasicAWSCredentials(configuration.AwsS3AccessKey, configuration.AwsS3SecretKey);
        var s3Client = new AmazonS3Client(awsCredentials, RegionEndpoint.USEast1);
        var fileTransferUtility = new TransferUtility(s3Client);
        var contentId = Guid.NewGuid();
        var contentPreloadId = Guid.NewGuid();
        var contentFile = contentId.ToString("N")+".mp4";
        var encryptedContentFile = contentFile + ".enc";
        var contentPreloadFile = contentPreloadId.ToString("N")+".mp4";
        var encryptedContentPreloadFile = contentPreloadFile + ".enc";
        var encryptionKey = Cryptography.GetBytes();

        var contentStatus = MediaExtensions.TranscodeToH264(uploadedFile, contentFile);
        var preloadStatus = MediaExtensions.TranscodeToH264(uploadedFile, contentPreloadFile, clipLengthSeconds: 60);
        while (!contentStatus.Complete || !preloadStatus.Complete) { Thread.Sleep(1); } //TODO: Iterate on this.

        Cryptography.EncryptFile(contentFile, encryptedContentFile, encryptionKey);
        Cryptography.EncryptFile(contentPreloadFile, encryptedContentPreloadFile, encryptionKey);

        var contentUploadRequest = new TransferUtilityUploadRequest
        {
            BucketName = configuration.AwsS3BucketName,
            FilePath = encryptedContentFile,
            Key = contentId.ToString("N"),
            StorageClass = S3StorageClass.Standard, //TODO: Modify to GlacierInstantRetrieval once we are stable.
            PartSize = 10 * 1024 * 1024,
            CannedACL = S3CannedACL.Private,
        };

        fileTransferUtility.Upload(contentUploadRequest);

        var contentPreloadUploadRequest = new TransferUtilityUploadRequest
        {
            BucketName = configuration.AwsS3BucketName,
            FilePath = encryptedContentPreloadFile,
            Key = contentPreloadId.ToString("N"),
            StorageClass = S3StorageClass.Standard, //TODO: Modify to GlacierInstantRetrieval once we are stable.
            PartSize = 10 * 1024 * 1024,
            CannedACL = S3CannedACL.Private,
        };

        fileTransferUtility.Upload(contentPreloadUploadRequest);

        File.Delete(uploadedFile);
        File.Delete(contentFile);
        File.Delete(contentPreloadFile);
        File.Delete(encryptedContentFile);
        File.Delete(encryptedContentPreloadFile);

        var media = new Media
        {
            ContentId = contentId,
            ContentPreloadId = contentPreloadId,
            EncryptionKey = encryptionKey,
            Title = title,
        };

        media.Id = sql.ExecuteScalar<long>(@$"
            INSERT INTO [{MediaTable.TableName}]
            (
                [{nameof(MediaTable.Title)}],
                [{nameof(MediaTable.EncryptionKey)}],
                [{nameof(MediaTable.ContentId)}],
                [{nameof(MediaTable.ContentPreloadId)}]
            )
            VALUES 
            (
                @{nameof(MediaTable.Title)},
                @{nameof(MediaTable.EncryptionKey)},
                @{nameof(MediaTable.ContentId)},
                @{nameof(MediaTable.ContentPreloadId)}
            )", Map(media));

        return media;
    }

    public static MediaTable Map(Media media) => new MediaTable
    {
        Id = media.Id,
        ContentId = media.ContentId,
        ContentPreloadId = media.ContentPreloadId,
        EncryptionKey = media.EncryptionKey,
        IsPending = media.IsPending,
        Title = media.Title
    };

    public static Media Map(MediaTable media) => new Media
    {
        Id = media.Id,
        ContentId = media.ContentId,
        ContentPreloadId = media.ContentPreloadId,
        EncryptionKey = media.EncryptionKey,
        IsPending = media.IsPending,
        Title = media.Title
    };

    public static List<Media> Map(IEnumerable<MediaTable> media) => media.Select(Map).ToList();
}
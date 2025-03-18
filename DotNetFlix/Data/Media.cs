using System.Diagnostics;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Transfer;
using Dapper;
using Microsoft.Data.Sqlite;

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

public static class MediaExtensions
{
    public static void WarmupMedia(this SqliteConnection sql, long mediaId)
    {
        //TODO: 
        //0. Check if IsPending=1 and skip item 2/3/4 if it is. 
        //1. Set IsPending = 1 for this media item. Use txn scope around 0/1.
        //2. Begin pulling the main content asynchronously on a background task. This task should set IsPending=0 before returning.
        //3. Pull the content preload synchronously on this thread.
        //4. Decrypt content preload & place in cache folder.
        //5. Return to caller. Ideally this whole process completes synchronously under ~3 seconds.
    }

    public static Media CreateMedia(this SqliteConnection sql, string uploadedFile, string title)
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

        //TODO: Eventually this should run async as something like a MediaCreationJob we can review from UI.

        TranscodeToH264(uploadedFile, contentFile);
        TranscodeToH264(uploadedFile, contentPreloadFile, clipLengthSeconds: 60);

        Cryptography.EncryptFile(contentFile, encryptedContentFile, encryptionKey);
        Cryptography.EncryptFile(contentPreloadFile, encryptedContentPreloadFile, encryptionKey);

        var uploadRequest = new TransferUtilityUploadRequest
        {
            BucketName = "test",
            FilePath = contentFile,
            Key = contentId.ToString("N"),
            StorageClass = S3StorageClass.Standard,
            PartSize = 10 * 1024 * 1024,
            CannedACL = S3CannedACL.Private,
        };

        fileTransferUtility.Upload(uploadRequest);

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

        media.Id = sql.ExecuteScalar<long>("@$TODO!");
        return media;
    }

    static void TranscodeToH264(string inputPath, string outputPath, int? clipLengthSeconds = null)
    {
        string durationArg = clipLengthSeconds.HasValue ? $"-t {clipLengthSeconds.Value}" : string.Empty;

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-i \"{inputPath}\" {durationArg} -c:v libx264 -preset slow -crf 27 -c:a aac -b:a 192k -movflags +faststart \"{outputPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = new Process { StartInfo = startInfo })
        {
            process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
            process.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }

        Console.WriteLine("Transcoding complete!");
    }
}
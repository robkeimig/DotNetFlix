using System.Buffers;
using System.Data.SQLite;
using Amazon.Runtime;
using Amazon.S3;
using Amazon;
using Amazon.S3.Model;

namespace DotNetFlix.Data;

public class MediaBlock
{
    public long Id { get; set; }
    public long MediaId { get; set; }
    public long Sequence { get; set; }
    public long Size { get; set; }
    public DateTime? LastAccessUtc { get; set; }
    public bool IsCached { get; set; }
    public byte[] EncryptionKey { get; set; }
}

public class MediaBlocksTable
{
    public const string TableName = "MediaBlocks";
    public long Id { get; set; }
    public long MediaId { get; set; }
    public long Sequence { get; set; }
    public long Size { get; set; }
    public long? LastAccessUnixTimestamp { get; set; }
    public bool IsCached { get; set; }
    public byte[] EncryptionKey { get; set; }
}

public static class MediaBlockDataExtensions
{
    public const string CachePath = "Cache";
    public const int BlockSize = 1024 * 1024 * 50;

    public static void EnsureMediaBlockAvailable(this SQLiteConnection sql, long blockId)
    {
        //TODO: Check the cache to see if this block is loaded for use.
        //Update the last accessed
    }

    public static async Task<bool> CreateMediaBlock(this SQLiteConnection sql, long mediaId, long sequence, Stream data)
    {
        var endOfStream = false;
        var buffer = ArrayPool<byte>.Shared.Rent(BlockSize);
        int bytesRead = 0;

        while (bytesRead < BlockSize)
        {
            int read = data.Read(buffer, bytesRead, BlockSize - bytesRead);
            
            if (read == 0)
            {
                endOfStream = true;
                break;
            }

            bytesRead += read;
        }


        var configuration = sql.GetConfiguration();
        var awsCredentials = new BasicAWSCredentials(configuration.AwsS3AccessKey, configuration.AwsS3SecretKey);
        using var s3Client = new AmazonS3Client(awsCredentials, RegionEndpoint.USEast1);

        var mediaBlock = new MediaBlock
        {
            MediaId = mediaId,
            Sequence = sequence,
            Size = bytesRead,
            //EncryptionKey
        };

        var request = new PutObjectRequest
        {
            BucketName = configuration.AwsS3BucketName,
            Key = mediaBlock.Id.ToString(),
            StorageClass = S3StorageClass.Standard, //TODO: Modify to GlacierInstantRetrieval once we are stable.
            InputStream = new MemoryStream(buffer, 0, bytesRead),
            CannedACL = S3CannedACL.Private,
        };

        var response = await s3Client.PutObjectAsync(request);
        return endOfStream;
    }
}
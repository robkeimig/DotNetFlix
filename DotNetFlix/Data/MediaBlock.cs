using System.Buffers;
using System.Data.SQLite;
using Amazon.Runtime;
using Amazon.S3;
using Amazon;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Dapper;

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
    public static MediaBlock GetMediaBlock(this SQLiteConnection sql, long mediaId, long sequence)
    {
        var row = sql.QueryFirst<MediaBlocksTable>($@"
            SELECT  * 
            FROM    {MediaBlocksTable.TableName} 
            WHERE   [{nameof(MediaBlocksTable.MediaId)}] = @{nameof(MediaBlocksTable.MediaId)}
            AND     [{nameof(MediaBlocksTable.Sequence)}] = @{nameof(MediaBlocksTable.Sequence)}", new
        {
            MediaId = mediaId,
            Sequence =sequence
        });

        return Map(row);
    }

    public static async Task LoadMediaBlock(this SQLiteConnection sql, long id)
    {
        using var transaction = await sql.BeginTransactionAsync();

        var mediaBlockRow = await sql.QueryFirstAsync<MediaBlocksTable>($@"
            SELECT  * 
            FROM    {MediaBlocksTable.TableName} 
            WHERE   [{nameof(MediaBlocksTable.Id)}] = @{nameof(MediaBlocksTable.Id)}", new
        {
            Id = id
        }, transaction);

        var mediaBlock = Map(mediaBlockRow);

        await sql.ExecuteAsync($@"UPDATE {MediaBlocksTable.TableName}
            SET [{nameof(MediaBlocksTable.IsCached)}] = 1,
                [{nameof(MediaBlocksTable.LastAccessUnixTimestamp)}] = @{nameof(MediaBlocksTable.LastAccessUnixTimestamp)}
            WHERE   [{nameof(MediaBlocksTable.Id)}] = @{nameof(MediaBlocksTable.Id)}", new
        {
            Id = mediaBlock.Id,
            LastAccessUnixTimestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
        }, transaction);

        if (!mediaBlock.IsCached)
        {
            var encryptedMediaBlockBuffer = ArrayPool<byte>.Shared.Rent(Constants.MediaBlockSize + 256);
            var mediaBlockFile = Path.Combine(Constants.MediaBlockCachePath, id.ToString());
            var configuration = sql.GetConfiguration();
            var s3ObjectName = id.ToString();
            var awsCredentials = new BasicAWSCredentials(configuration.AwsS3AccessKey, configuration.AwsS3SecretKey);
            var s3Client = new AmazonS3Client(awsCredentials, RegionEndpoint.USEast1);
            var getObjectRequest = new GetObjectRequest
            {
                BucketName = configuration.AwsS3BucketName,
                Key = s3ObjectName,
            };

            var mediaBlockStream = new FileStream(mediaBlockFile, FileMode.Create, FileAccess.Write, FileShare.Read);
            
            var getObjectResponse = await s3Client.GetObjectAsync(getObjectRequest);
            var encryptedMediaBlockStream = new MemoryStream(encryptedMediaBlockBuffer, 0, (int)getObjectResponse.ContentLength);
            await getObjectResponse.ResponseStream.CopyToAsync(encryptedMediaBlockStream);
            encryptedMediaBlockStream.Position = 0;

            Cryptography.DecryptStream(encryptedMediaBlockStream, mediaBlockStream, mediaBlock.EncryptionKey);

            encryptedMediaBlockStream.Dispose();
            mediaBlockStream.Close();
            mediaBlockStream.Dispose();
            ArrayPool<byte>.Shared.Return(encryptedMediaBlockBuffer);
            Console.WriteLine($@"Cached Media Block {mediaBlock.Id}");
        }

        await transaction.CommitAsync();
        await transaction.DisposeAsync();
    }

    public static void ShrinkMediaBlockCache(this SQLiteConnection sql)
    {
        var configuration = sql.GetConfiguration();
        
        var totalSize = sql.ExecuteScalar<long>($@"
            SELECT  SUM([{nameof(MediaBlocksTable.Size)}]) 
            FROM    {MediaBlocksTable.TableName}
            WHERE   [{nameof(MediaBlocksTable.IsCached)}] = 1");

        if (totalSize > configuration.CacheSize)
        {
            var mediaBlockRow = sql.QueryFirst<MediaBlocksTable>($@"
                    SELECT      *
                    FROM        {MediaBlocksTable.TableName} 
                    WHERE       [{nameof(MediaBlocksTable.IsCached)}] = 1
                    ORDER BY    [{nameof(MediaBlocksTable.LastAccessUnixTimestamp)}] ASC 
                    LIMIT       1");

            var mediaBlock = Map(mediaBlockRow);

            sql.Execute($@"
                UPDATE {MediaBlocksTable.TableName}
                SET     [{nameof(MediaBlocksTable.IsCached)}] = 0
                WHERE [{nameof(MediaBlocksTable.Id)}] = @{nameof(MediaBlocksTable.Id)}", new
            {
                Id = mediaBlock.Id
            });

            File.Delete(Path.Combine(Constants.MediaBlockCachePath, mediaBlock.Id.ToString()));
            Console.WriteLine($"Expired Media Block {mediaBlock.Id} from cache.");
        }   
    }

    public static async Task<bool> CreateMediaBlock(this SQLiteConnection sql, long mediaId, long sequence, Stream data)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(Constants.MediaBlockSize);
        var encryptedBuffer = ArrayPool<byte>.Shared.Rent(Constants.MediaBlockSize + 256); //Leave some room for the IV & padding.
        int bytesRead = 0;
        var hasMoreData = true;

        while (bytesRead < Constants.MediaBlockSize)
        {
            int read = data.Read(buffer, bytesRead, Constants.MediaBlockSize - bytesRead);
            
            if (read == 0)
            {
                hasMoreData = false;
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
            EncryptionKey = Cryptography.GenerateEncryptionKey(),
        };

        mediaBlock.Id = await sql.ExecuteScalarAsync<long>($@"
            INSERT INTO {MediaBlocksTable.TableName}
            (
                [{nameof(MediaBlocksTable.MediaId)}],
                [{nameof(MediaBlocksTable.Sequence)}],
                [{nameof(MediaBlocksTable.Size)}],
                [{nameof(MediaBlocksTable.EncryptionKey)}]
            )
            VALUES 
            (
                @{nameof(MediaBlocksTable.MediaId)},
                @{nameof(MediaBlocksTable.Sequence)},
                @{nameof(MediaBlocksTable.Size)},
                @{nameof(MediaBlocksTable.EncryptionKey)}
            )
            RETURNING [{nameof(MediaBlocksTable.Id)}]
        ", Map(mediaBlock));

        var encryptedMemoryStream = new MemoryStream(encryptedBuffer);
        Cryptography.EncryptStream(new MemoryStream(buffer, 0, bytesRead), encryptedMemoryStream, mediaBlock.EncryptionKey);
        var finalStream = new MemoryStream(encryptedBuffer, 0, (int)encryptedMemoryStream.Position);

        var request = new PutObjectRequest
        {
            BucketName = configuration.AwsS3BucketName,
            Key = mediaBlock.Id.ToString(),
            StorageClass = S3StorageClass.Standard, //TODO: Modify to GlacierInstantRetrieval once we are stable.
            InputStream = finalStream,
            CannedACL = S3CannedACL.Private,
        };

        var response = await s3Client.PutObjectAsync(request);

        encryptedMemoryStream.Dispose();
        finalStream.Dispose();
        ArrayPool<byte>.Shared.Return(buffer);
        ArrayPool<byte>.Shared.Return(encryptedBuffer);

        return hasMoreData;
    }

    public static MediaBlock Map(MediaBlocksTable mediaBlock) => new MediaBlock
    {
        Id = mediaBlock.Id,
        MediaId = mediaBlock.MediaId,
        Sequence  = mediaBlock.Sequence,
        Size = mediaBlock.Size,
        IsCached = mediaBlock.IsCached,
        LastAccessUtc = mediaBlock.LastAccessUnixTimestamp.HasValue ? DateTimeOffset.FromUnixTimeSeconds(mediaBlock.LastAccessUnixTimestamp.Value).UtcDateTime : null,
        EncryptionKey = mediaBlock.EncryptionKey,
    };

    public static MediaBlocksTable Map(MediaBlock mediaBlock) => new MediaBlocksTable
    {
        Id = mediaBlock.Id,
        MediaId = mediaBlock.MediaId,
        Sequence = mediaBlock.Sequence,
        Size = mediaBlock.Size,
        IsCached = mediaBlock.IsCached,
        LastAccessUnixTimestamp = mediaBlock.LastAccessUtc.HasValue ? new DateTimeOffset(mediaBlock.LastAccessUtc.Value).ToUnixTimeSeconds() : null,
        EncryptionKey = mediaBlock.EncryptionKey,
    };
}
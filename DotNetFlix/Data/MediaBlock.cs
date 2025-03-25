using System.Buffers;
using System.Data.SQLite;
using Amazon.Runtime;
using Amazon.S3;
using Amazon;
using Amazon.S3.Model;
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
            WHERE   [{nameof(MediaBlocksTable.MediaId)}] = @{nameof(MediaBlocksTable.MediaId)},
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
            WHERE   [{nameof(MediaBlocksTable.Id)}] = @{nameof(MediaBlocksTable.Id)}
            FOR     UPDATE", new
        {
            Id = id
        }, transaction);

        var mediaBlock = Map(mediaBlockRow);
        
        if (mediaBlock.IsCached)
        {
            await transaction.RollbackAsync();
            await transaction.DisposeAsync();
            return;
        }

        throw new NotImplementedException("TODO: Pull the block from S3, decrypt it and place it in cache folder.");
        throw new NotImplementedException("TODO: Separately in the main management thread on a less frequent basis we perform expiration of cache entries.");

        await sql.ExecuteAsync($@"UPDATE {MediaBlocksTable.TableName}
            SET [{nameof(MediaBlocksTable.IsCached)}] = 1,
                [{nameof(MediaBlocksTable.LastAccessUnixTimestamp)}] = @{nameof(MediaBlocksTable.LastAccessUnixTimestamp)}
            WHERE   [{nameof(MediaBlocksTable.Id)}] = @{nameof(MediaBlocksTable.Id)}", new
        {
            Id = mediaBlock.Id,
            LastAccessUnixTimestamp = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds(),
        }, transaction);

        await transaction.CommitAsync();
        await transaction.DisposeAsync();
    }

    public static async Task<bool> CreateMediaBlock(this SQLiteConnection sql, long mediaId, long sequence, Stream data)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(Constants.MediaBlockSize);
        int bytesRead = 0;
        var endOfStream = false;

        while (bytesRead < Constants.MediaBlockSize)
        {
            int read = data.Read(buffer, bytesRead, Constants.MediaBlockSize - bytesRead);
            
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
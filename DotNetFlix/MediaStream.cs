using System.Data.SQLite;
using DotNetFlix.Data;

namespace DotNetFlix;

internal class MediaStream : Stream
{
    readonly long _length;
    SQLiteConnection _sql;
    long _mediaId;
    long _position;
    FileStream? _currentMediaBlockStream;
    long _currentMediaBlockSequence;

    public MediaStream(SQLiteConnection sql, long mediaId)
    {
        _sql = sql;
        _mediaId = mediaId;
        _currentMediaBlockSequence = -1;
    }

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => false;

    public override long Length => _length;

    public override long Position 
    {
        get => _position;

        set 
        { 
            if (value < 0 || value > _length)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _position = value;
        }
    }

    public override void Flush()
    {
        return;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException("This type only supports asynchronous read operations.");
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
            throw new ArgumentException("Invalid buffer offset or count.");

        int totalBytesRead = 0;

        while (count > 0 && _position < _length)
        {
            int mediaBlockSequence = (int)(_position / Constants.MediaBlockSize);
            int mediaBlockOffset = (int)(_position % Constants.MediaBlockSize);

            if (mediaBlockSequence != _currentMediaBlockSequence)
            {
                if (_currentMediaBlockStream != null)
                {
                    await _currentMediaBlockStream.DisposeAsync();
                }

                var mediaBlock = _sql.GetMediaBlock(_mediaId, mediaBlockSequence);
                await _sql.LoadMediaBlock(mediaBlock.Id);

                _currentMediaBlockStream = new FileStream(Path.Combine(Constants.MediaBlockCachePath, mediaBlock.Id.ToString()), FileMode.Open, FileAccess.Read, FileShare.Read);
                _currentMediaBlockSequence = mediaBlockSequence;
            }

            _currentMediaBlockStream.Seek(mediaBlockOffset, SeekOrigin.Begin);

            int bytesAvailable = (int)Math.Min(Constants.MediaBlockSize - mediaBlockOffset, count);
            int bytesToRead = (int)Math.Min(bytesAvailable, _length - _position);
            int bytesRead = await _currentMediaBlockStream.ReadAsync(buffer.AsMemory(offset, bytesToRead), cancellationToken);
            if (bytesRead == 0) break; 

            _position += bytesRead;
            offset += bytesRead;
            count -= bytesRead;
            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentException("Unknown SeekOrigin.", nameof(origin))
        };

        if (newPosition < 0 || newPosition > _length)
        {
            throw new ArgumentException("Outside bounds.", nameof(offset));
        }
            
        _position = newPosition;
        return _position;
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException("Cannot set the length of streams of this type.");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException("Cannot write to streams of this type.");
    }
}

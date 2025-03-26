using System.Data.SQLite;
using DotNetFlix.Data;

namespace DotNetFlix;

internal class MediaStream : Stream
{
    readonly long _length;
    readonly SQLiteConnection _sql;
    readonly long _mediaId;
    bool _disposed = false; 

    long _position;
    FileStream? _mediaBlockStream;
    long _mediaBlockSequence;

    public MediaStream(SQLiteConnection sql, long mediaId)
    {
        _sql = sql;
        _mediaId = mediaId;
        _mediaBlockSequence = -1;

        var media = sql.GetMedia(mediaId);
        _length = media.Size;
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

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            if (_mediaBlockStream != null)
            {
                _mediaBlockStream.Close();
                _mediaBlockStream.Dispose();
            }
        }

        _disposed = true;
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

            if (mediaBlockSequence != _mediaBlockSequence)
            {
                if (_mediaBlockStream != null)
                {
                    _mediaBlockStream.Close();
                    await _mediaBlockStream.DisposeAsync();
                }

                var mediaBlock = _sql.GetMediaBlock(_mediaId, mediaBlockSequence);
                await _sql.LoadMediaBlock(mediaBlock.Id);

                _mediaBlockStream = new FileStream(Path.Combine(Constants.MediaBlockCachePath, mediaBlock.Id.ToString()), FileMode.Open, FileAccess.Read, FileShare.Read);
                _mediaBlockSequence = mediaBlockSequence;
            }

            _mediaBlockStream.Seek(mediaBlockOffset, SeekOrigin.Begin);

            int bytesAvailable = (int)Math.Min(Constants.MediaBlockSize - mediaBlockOffset, count);
            int bytesToRead = (int)Math.Min(bytesAvailable, _length - _position);
            int bytesRead = await _mediaBlockStream.ReadAsync(buffer.AsMemory(offset, bytesToRead), cancellationToken);
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

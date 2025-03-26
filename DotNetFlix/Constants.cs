namespace DotNetFlix;

internal class Constants
{
    public const int UploadPartSize = 1024 * 1024 * 5;
    public const long MaximumUploadSize = 1024L * 1024L * 1024L * 32L;
    public const long DefaultCacheSize = 1024L * 1024L * 1024L * 10L; 
    public const string MediaBlockCachePath = "MediaBlockCache";
    public const string VideoContentType = "video/mp4";
    public const int MediaBlockSize = 1024 * 1024 * 50;
}

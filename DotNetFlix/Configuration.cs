using DotNetFlix.Data;
using System.Data.SQLite;

namespace DotNetFlix;

public class Configuration
{
    public string AwsS3AccessKey { get; set; }      
    public string AwsS3SecretKey { get; set; }
    public string AwsS3BucketName { get; set; }
    public string FfmpegPath { get; set; }
    public long CacheSize { get; set; }
}

public static class ConfigurationExtensions
{
    public static Configuration GetConfiguration(this SQLiteConnection sql)
    {
        var settings = sql.GetSettings();

        return new Configuration
        {
            AwsS3AccessKey = settings.First(x => x.Key == nameof(Configuration.AwsS3AccessKey)).Value,
            AwsS3SecretKey = settings.First(x => x.Key == nameof(Configuration.AwsS3SecretKey)).Value,
            AwsS3BucketName = settings.First(x => x.Key == nameof(Configuration.AwsS3BucketName)).Value,
            FfmpegPath = settings.First(x => x.Key == nameof(Configuration.FfmpegPath)).Value,
            CacheSize = long.Parse(settings.First(x => x.Key == nameof(Configuration.CacheSize)).Value)
        };
    }
}

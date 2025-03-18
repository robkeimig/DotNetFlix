using Microsoft.Data.Sqlite;
using DotNetFlix.Data;

namespace DotNetFlix;

public class Configuration
{
    public byte[] MasterEncryptionKey { get; set; }
    public string AwsS3AccessKey { get; set; }      
    public string AwsS3SecretKey { get; set; }
    public string FfmpegPath { get; set; }
}

public static class ConfigurationExtensions
{
    public static Configuration GetConfiguration(this SqliteConnection sql)
    {
        var settings = sql.GetSettings();

        return new Configuration
        {
            MasterEncryptionKey = Convert.FromBase64String(settings.First(x => x.Key == nameof(Configuration.MasterEncryptionKey)).Value),
            AwsS3AccessKey = settings.First(x=>x.Key == nameof(Configuration.AwsS3AccessKey)).Value,
            AwsS3SecretKey = settings.First(x => x.Key == nameof(Configuration.AwsS3SecretKey)).Value,
            FfmpegPath = settings.First(x => x.Key == nameof(Configuration.FfmpegPath)).Value,
        };
    }
}

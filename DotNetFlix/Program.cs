using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using DotNetFlix;
using DotNetFlix.Data;
using Microsoft.Data.Sqlite;

if (!File.Exists($"{nameof(SystemPassword)}.json"))
{
    Console.WriteLine($"{nameof(SystemPassword)}.json file missing! It needs to be in the same directory as the executable.");
    return;
}

var systemPasswordJson = File.ReadAllText(nameof(SystemPassword)+".json");
var systemPassword = JsonSerializer.Deserialize<SystemPassword>(systemPasswordJson);
var sql = new SqliteConnection("Data Source = data.db");
sql.EnsureSchema();
sql.InitializeSettings();
var configuration = sql.GetConfiguration();
var webServer = new WebServer();

var awsCredentials = new BasicAWSCredentials(configuration.AwsS3AccessKey, configuration.AwsS3SecretKey);
var s3Client = new AmazonS3Client(awsCredentials, RegionEndpoint.USEast1);

while (true)
{
    Thread.Sleep(1);
}
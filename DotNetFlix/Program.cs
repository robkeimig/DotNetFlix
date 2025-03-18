using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

string accessKey = Environment.GetEnvironmentVariable("AwsS3AccessKey") ?? throw new Exception("Not Specified.");
string secretKey = Environment.GetEnvironmentVariable("AwsS3SecretAccessKey") ?? throw new Exception("Not Specified.");
var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);
var s3Client = new AmazonS3Client(awsCredentials, RegionEndpoint.USEast1);

try
{
    var response = await s3Client.ListBucketsAsync();

    Console.WriteLine("Your S3 Buckets:");
    foreach (S3Bucket bucket in response.Buckets)
    {
        Console.WriteLine($"- {bucket.BucketName} (Created on {bucket.CreationDate})");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error listing buckets: {ex.Message}");
}
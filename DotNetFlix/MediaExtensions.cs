using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace DotNetFlix;

internal static class MediaExtensions
{
    public static async Task ServeRangeVideoContent(HttpContext httpContext, Stream stream, long contentLength, string contentType)
    {
        var rangeHeader = httpContext.Request.Headers.Range;

        if (stream == null || contentLength <= 0)
        {
            httpContext.Response.StatusCode = 404;
            return;
        }

        if (string.IsNullOrEmpty(rangeHeader))
        {
            await ServeFullStream(httpContext, stream, contentLength, contentType);
            return;
        }

        var range = ParseRange(rangeHeader, contentLength);

        if (range == null || range?.Start >= contentLength || range?.End >= contentLength || range?.Start > range?.End)
        {
            httpContext.Response.StatusCode = 416;
            httpContext.Response.Headers.ContentRange = $"bytes */{contentLength}";
            return;
        }

        await ServeByteRangeFromStream(httpContext, stream, range.Value.Start, range.Value.End, contentLength, contentType);
    }

    private static async Task ServeFullStream(HttpContext httpContext, Stream stream, long contentLength, string contentType)
    {
        httpContext.Response.StatusCode = 200;
        httpContext.Response.ContentType = contentType;
        httpContext.Response.ContentLength = contentLength;

        stream.Seek(0, SeekOrigin.Begin);
        await stream.CopyToAsync(httpContext.Response.Body);
    }

    private static async Task ServeByteRangeFromStream(HttpContext httpContext, Stream stream, long start, long end, long totalLength, string contentType)
    {
        var length = end - start + 1;
        httpContext.Response.StatusCode = 206;
        httpContext.Response.ContentType = contentType;
        httpContext.Response.Headers.ContentRange = $"bytes {start}-{end}/{totalLength}";
        httpContext.Response.ContentLength = length;

        stream.Seek(start, SeekOrigin.Begin);

        byte[] buffer = new byte[81920]; // 80KB buffer size
        long remaining = length;
        while (remaining > 0)
        {
            int read = await stream.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0) break;

            await httpContext.Response.Body.WriteAsync(buffer, 0, read);
            remaining -= read;
        }
    }

    public static async Task ServeRangeVideoContent(HttpContext httpContext, string file)
    {
        var fileLength = new FileInfo(file).Length;
        var rangeHeader = httpContext.Request.Headers.Range;

        if (!File.Exists(file))
        {
            httpContext.Response.StatusCode = 404; 
            return;
        }

        if (string.IsNullOrEmpty(rangeHeader))
        {
            await ServeFullFile(httpContext, file, fileLength);
            return;
        }

        var range = ParseRange(rangeHeader, fileLength);

        if (range == null || range?.Start >= fileLength || range?.End >= fileLength || range?.Start > range?.End)
        {
            httpContext.Response.StatusCode = 416;
            httpContext.Response.Headers.ContentRange = $"bytes */{fileLength}";
            return;
        }

        await ServeByteRange(httpContext, file, range.Value.Start, range.Value.End, fileLength);
    }

    private static (long Start, long End)? ParseRange(string rangeHeader, long fileLength)
    {
        if (rangeHeader.StartsWith("bytes="))
        {
            string rangeValue = rangeHeader.Substring(6);
            string[] rangeParts = rangeValue.Split('-');

            if (rangeParts.Length == 2)
            {
                long start = -1;
                long end = -1;

                if (long.TryParse(rangeParts[0], out start))
                {
                    if (!string.IsNullOrEmpty(rangeParts[1]) && long.TryParse(rangeParts[1], out end))
                    {
                        if (start >= 0 && end >= start && end < fileLength)
                        {
                            return (start, end);
                        }
                    }
                    else if (start >= 0 && start < fileLength)
                    {
                        end = fileLength - 1; 
                        return (start, end);
                    }
                }
            }
        }
        return null;
    }

    static async Task ServeFullFile(HttpContext httpContext, string filePath, long fileLength)
    {
        httpContext.Response.StatusCode = 200;
        httpContext.Response.ContentType = Constants.VideoContentType;
        httpContext.Response.ContentLength = fileLength;
        using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        await fs.CopyToAsync(httpContext.Response.Body);
    }

    private static async Task ServeByteRange(HttpContext httpContext, string filePath, long start, long end, long fileLength)
    {
        var rangeLength = end - start + 1;
        httpContext.Response.StatusCode = 206; 
        httpContext.Response.ContentType = Constants.VideoContentType; 
        httpContext.Response.ContentLength = rangeLength;
        httpContext.Response.Headers.ContentRange = $"bytes {start}-{end}/{fileLength}";
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        fs.Seek(start, SeekOrigin.Begin);
        byte[] buffer = new byte[8192]; 
        long bytesRead;
        long bytesToRead = rangeLength;

        while ((bytesRead = fs.Read(buffer, 0, (int)Math.Min(buffer.Length, bytesToRead))) > 0)
        {
            await httpContext.Response.Body.WriteAsync(buffer.AsMemory(0, (int)bytesRead));
            bytesToRead -= bytesRead;
            if (bytesToRead <= 0)
                break;
        }
    }

    public static TranscodingJobStatus TranscodeToH264(string inputPath, string outputPath, int? clipLengthSeconds = null, int? startTimeSeconds = 0, int? audioBitRate = 192, int constantRateFactor = 22)
    {
        var transcodingJobStatus = new TranscodingJobStatus();

        Task.Run(() =>
        {
            string durationArg = clipLengthSeconds.HasValue ? $"-t {clipLengthSeconds.Value}" : string.Empty;
            var mediaInformation = GetMediaInformation(inputPath);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-ss {startTimeSeconds} -i \"{inputPath}\" {durationArg} -c:v libx264 -preset slow -crf {constantRateFactor} -c:a aac -b:a {audioBitRate}k -movflags +faststart \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
                
                process.ErrorDataReceived += (sender, e) =>
                {
                    //Parse current ffmpeg timestamp using regex pattern.
                    //Convert to current total seconds.
                    //Divide current total seconds by mediaInformation.TotalSeconds.
                    transcodingJobStatus.Percentage = 0d / 1d;
                    Console.WriteLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            }

            transcodingJobStatus.Complete = true;
        });

        return transcodingJobStatus;
    }

    public class TranscodingJobStatus 
    {
        public bool Complete;
        public double Percentage;
    }


    public class MediaInformation
    {
        public int LengthSeconds { get; set; }
        public int AudioBitRate { get; set; }
        public int VideoWidthPixels { get; set; }
        public int VideoHeightPixels { get; set; }
        public int VideoFrameRate { get; set; }
    }

    public static MediaInformation GetMediaInformation(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"The file {filePath} does not exist.");
        }

        var mediaInfo = new MediaInformation();
        var ffmpegProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-i \"{filePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        using (ffmpegProcess)
        {
            ffmpegProcess.Start();
            string output = ffmpegProcess.StandardError.ReadToEnd();
            ffmpegProcess.WaitForExit();
            mediaInfo.LengthSeconds = ParseLengthSeconds(output);
            mediaInfo.AudioBitRate = ParseAudioBitRate(output);
            mediaInfo.VideoWidthPixels = ParseVideoWidth(output);
            mediaInfo.VideoHeightPixels = ParseVideoHeight(output);
            mediaInfo.VideoFrameRate = ParseVideoFrameRate(output);
        }

        return mediaInfo;
    }

    static int ParseLengthSeconds(string output)
    {
        var match = System.Text.RegularExpressions.Regex.Match(output, @"Duration: (\d+):(\d+):(\d+\.\d+)");
        if (match.Success)
        {
            int hours = int.Parse(match.Groups[1].Value);
            int minutes = int.Parse(match.Groups[2].Value);
            double seconds = double.Parse(match.Groups[3].Value);

            return (int)(hours * 3600 + minutes * 60 + seconds);
        }

        return 0;
    }

    static int ParseAudioBitRate(string output)
    {
        var match = System.Text.RegularExpressions.Regex.Match(output, @"Audio:.*?, (\d+) kb/s");
        if (match.Success)
        {
            return int.Parse(match.Groups[1].Value);
        }

        return 0;
    }

    static int ParseVideoWidth(string output)
    {
        var match = System.Text.RegularExpressions.Regex.Match(output, @", (\d+)x(\d+),");
        if (match.Success)
        {
            return int.Parse(match.Groups[1].Value);
        }

        return 0;
    }

    static int ParseVideoHeight(string output)
    {
        var match = System.Text.RegularExpressions.Regex.Match(output, @", (\d+)x(\d+)");
        if (match.Success)
        {
            return int.Parse(match.Groups[2].Value);
        }

        return 0;
    }

    static int ParseVideoFrameRate(string output)
    {
        var match = System.Text.RegularExpressions.Regex.Match(output, @"(\d+(\.\d+)?) fps");
        if (match.Success)
        {
            return (int)float.Parse(match.Groups[1].Value);
        }

        return 0;
    }
}

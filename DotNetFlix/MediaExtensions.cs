using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace DotNetFlix;

internal static class MediaExtensions
{
    const string ContentType = "video/mp4";

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
        httpContext.Response.ContentType = ContentType;
        httpContext.Response.ContentLength = fileLength;
        using FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        await fs.CopyToAsync(httpContext.Response.Body);
    }

    private static async Task ServeByteRange(HttpContext httpContext, string filePath, long start, long end, long fileLength)
    {
        var rangeLength = end - start + 1;
        httpContext.Response.StatusCode = 206; 
        httpContext.Response.ContentType = ContentType; 
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

    public static void TranscodeToH264(string inputPath, string outputPath, int? clipLengthSeconds = null, int? startTimeSeconds = 0, int? audioBitRate = 192, int constantRateFactor = 22)
    {
        string durationArg = clipLengthSeconds.HasValue ? $"-t {clipLengthSeconds.Value}" : string.Empty;

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
            process.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }
    }
}

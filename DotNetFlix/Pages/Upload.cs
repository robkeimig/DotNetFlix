using System.Data.SQLite;
using System.Text;
using System.Text.Json;
using DotNetFlix.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DotNetFlix.Pages;

internal class Upload : Page
{
    private const string FileUploadIdKey = "FileUploadId";
    const string UploadFileNameKey = "UploadFileName";
    const string PreviewFileNameKey = "PreviewFileName";

    const int UploadPartSize = 1024 * 1024 * 5;

    const string FileInputElement = "File";
    const string StartTimeSecondsElement = "StartTimeSeconds";
    const string FileName = "FileName";
    const string FileSize = "FileSize";
    const string FilePartSequence = "FilePartSequence";
    const string FilePartData = "FilePartData";
    const string UploadButtonElement = "UploadButton";
    const string UploadPartsElement = "UploadParts";
    const string UploadProgressElement = "UploadProgress";
    const string CancelAction = "Cancel";
    const string BeginUploadAction = "BeginUpload";
    const string UploadPartAction = "UploadPart";
    const string CompleteUploadAction = "CompleteUpload";
    const string GeneratePreviewAction = "GeneratePreview";
    const string CreateAction = "Confirm";

    enum ViewMode
    {
        Default,
        UploadComplete
    }

    public override async Task Get(HttpContext context, SQLiteConnection sql, long sessionId)
    {
        var session = sql.GetSession(sessionId);

        if (context.Request.Path.StartsWithSegments("/Preview"))
        {
            var previewFileName = sql.GetSessionData(sessionId, PreviewFileNameKey);

            if (!System.IO.File.Exists(previewFileName))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var fileInfo = new FileInfo(previewFileName);

            await context.Response.SendFileAsync(previewFileName);  //Can we support seeking here?
            return;
            //// The method to return the file
            //var fileResult = new FileStreamResult(new FileStream(previewFileName, FileMode.Open, FileAccess.Read, FileShare.Read), "video/mp4")
            //{
            //    FileDownloadName = Path.GetFileName(previewFileName)
            //};

            //// Ensure ASP.NET Core handles range requests and other headers automatically
            //fileResult.EnableRangeProcessing = true;

            //// You can return the file directly to the response
            //await fileResult.ExecuteResultAsync(context);
        }

        _ = Enum.TryParse(sql.GetSessionData(sessionId, nameof(ViewMode)), out ViewMode viewMode);
        FileUpload? fileUpload = default;

        if (viewMode == ViewMode.UploadComplete)
        {
            var fileUploadIdString = sql.GetSessionData(sessionId, FileUploadIdKey);
            var fileUploadId = long.Parse(fileUploadIdString);
            fileUpload = sql.GetFileUpload(fileUploadId);
        }

        var view = HtmlTemplate(Html(viewMode, fileUpload), Css(), Js(viewMode));
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(view), context.RequestAborted);
    }

    public override async Task Post(HttpContext context, SQLiteConnection sql, long sessionId)
    {
        var session = sql.GetSession(sessionId);
        var form = await context.Request.ReadFormAsync();

        switch (form[Action])
        {
            case BeginUploadAction:
                {
                    var fileName = form[FileName];
                    var fileSizeString = form[FileSize];

                    if (!long.TryParse(fileSizeString, out long fileSize))
                    {
                        throw new Exception("Cannot parse file size.");
                    }

                    var totalParts = (int)Math.Ceiling((double)fileSize / UploadPartSize);

                    var beginUploadResponseJson = JsonSerializer.Serialize(new
                    {
                        partSize = UploadPartSize,
                        totalParts = totalParts
                    });

                    var file = sql.CreateFileUpload(sessionId, fileName, fileSize);
                    sql.SetSessionData(sessionId, FileUploadIdKey, file.Id.ToString());
                    context.Response.ContentType = "application/json";
                    await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(beginUploadResponseJson), context.RequestAborted);
                    break;
                }
            case UploadPartAction:
                {
                    var filePartSequenceString = form[FilePartSequence];
                    var file = form.Files[0];
                    

                    if (!long.TryParse(filePartSequenceString, out long filePartSequence))
                    {
                        throw new ArgumentException();
                    }

                    var fileUploadIdString = sql.GetSessionData(sessionId, FileUploadIdKey);
                    var fileUploadId = long.Parse(fileUploadIdString);
                    var fileUpload = sql.GetFileUpload(fileUploadId);
                    var extension = new FileInfo(fileUpload.Name).Extension;
                    using var fileStream = new FileStream(fileUploadId+extension, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write, 4096, true);
                    fileStream.Seek(filePartSequence * UploadPartSize, SeekOrigin.Begin);
                    await file.CopyToAsync(fileStream);
                    Console.WriteLine(filePartSequence);
                    break;
                }
            case CompleteUploadAction:
                {
                    sql.SetSessionData(sessionId, nameof(ViewMode), ViewMode.UploadComplete.ToString());
                    var fileUploadIdString = sql.GetSessionData(sessionId, FileUploadIdKey);
                    var fileUploadId = long.Parse(fileUploadIdString);
                    sql.SetFileUploadCompleted(fileUploadId);
                    var fileUpload = sql.GetFileUpload(fileUploadId);
                    var previewFileName = Guid.NewGuid().ToString("N") + ".mp4";
                    var extension = new FileInfo(fileUpload.Name).Extension;
                    var fileName = fileUploadId + extension;
                    MediaExtensions.TranscodeToH264(fileName, previewFileName, 30);
                    sql.SetSessionData(sessionId, PreviewFileNameKey, previewFileName);
                    await Get(context, sql, sessionId);
                    break;
                }
            case CancelAction:
                {
                    sql.ClearSessionData(sessionId);
                    sql.SetSessionPage(sessionId, nameof(Home));
                    await Instance(nameof(Home)).Get(context, sql, sessionId);
                    break;
                }
            case GeneratePreviewAction:
                {
                    var startTimeSecondsString = form[StartTimeSecondsElement];

                    if (!int.TryParse(startTimeSecondsString, out int startTimeSeconds))
                    {
                        throw new Exception("Cannot parse star time.");
                    }

                    var previewFileName = Guid.NewGuid().ToString("N") + ".mp4";
                    var fileUploadIdString = sql.GetSessionData(sessionId, FileUploadIdKey);
                    var fileUploadId = long.Parse(fileUploadIdString);
                    var fileUpload = sql.GetFileUpload(fileUploadId);
                    var extension = new FileInfo(fileUpload.Name).Extension;
                    var fileName = fileUploadId + extension;
                    MediaExtensions.TranscodeToH264(fileName, previewFileName, 30, startTimeSeconds);
                    sql.SetSessionData(sessionId, PreviewFileNameKey, previewFileName);
                    await Get(context, sql, sessionId);
                    break;
                }
            default:
                throw new NotImplementedException();
        }
    }

    string Html(ViewMode viewMode, FileUpload? fileUpload) => $@"
<form method=""POST"" enctype=""multipart/form-data"">
    <button type=""submit"" name=""{Action}"" value=""{CancelAction}"">Cancel</button>
</form>

<div id='content'>
    {(viewMode == ViewMode.Default ? $@"
        <label for=""{FileInputElement}"">File:</label>
        <input type=""file"" id=""{FileInputElement}"">
        <br><br>
        <button id=""{UploadButtonElement}"">Upload</button>
        <div id=""{UploadProgressElement}""></div>
        <div id=""{UploadPartsElement}""></div>
    " : string.Empty)}

    {(viewMode == ViewMode.UploadComplete ? $@"
        <h1 style='margin-top:0'>Uploaded File Information</h1>
        <table>
            <tr>
                <td><b>File Name</b></td>
                <td>{fileUpload.Name}</td>
            </tr>
            <tr>
                <td><b>File Size</b></td>
                <td>{fileUpload.Size}</td>
            </tr>
            <tr>
                <td><b>Upload Time</b></td>
                <td>{(fileUpload.UploadCompletedUtc.Value - fileUpload.CreatedUtc).TotalSeconds} seconds</td>
            </tr>
        </table>
<form method=""POST"" enctype=""multipart/form-data"">
        <h1>Media Information</h1>
        <b>Title</b>
        <input type='text' style='width:50%'/>
        <h1>Transcoding</h1>
        <p>Transcoding is always applied to standardize media for a guaranteed playback experience.</p>
        <p>You may adjust the below settings to better fit the content:</p>
        <table>
            <tr>
                <td><b>FFmpeg - Constant Rate Factor (CRF)</b></td>
                <td><input type='text' value='18'></td>
            </tr>
            <tr>
                <td><b>FFmpeg - Audio Bitrate (kbps)</b></td>
                <td><input type='text' value='192'></td>
            </tr>
        </table>
        <h1>Preview</h1>
        <p>Demonstrates the selected transcoding settings.</p>
        <b>Start Time (seconds)</b>
        <input type='text' name='{StartTimeSecondsElement}' value='0'>
        <button type='submit' name='{Action}' value='{GeneratePreviewAction}'>Preview</button>       
        <br>
        <br>
        <video src='/Preview' controls autoplay muted loop></video>
        <br>
        <button type='submit' name='{Action}' value='{CreateAction}'>Create</button>       
</form>
    " : string.Empty)}
</div>
";

    string Css() => $@"
#content {{
    border: 1px solid #000;
    padding: 1rem;
    margin: 1rem;
}}

 video {{
    width: 100%; 
    height: auto;
    display: block;
  }}
";
    string Js(ViewMode viewMode) => $@"

{(viewMode == ViewMode.Default ? $@"
document.getElementById('{UploadButtonElement}').addEventListener('click', async function () {{
    const fileInput = document.getElementById('{FileInputElement}');

    if (!fileInput.files.length) {{
        alert(""Please select a file to upload."");
        return;
    }}

    const file = fileInput.files[0];   

   
    // Step 1: Request upload session details using FormData
    const beginForm = new FormData();
    beginForm.append(""{Action}"", ""{BeginUploadAction}"");
    beginForm.append(""{FileName}"", file.name);
    beginForm.append(""{FileSize}"", file.size);

    const response = await fetch('/', {{
        method: 'POST',
        body: beginForm
    }});

    if (!response.ok) {{
        alert(""Failed to initiate upload."");
        return;
    }}

    const {{partSize,totalParts}} = await response.json();

    console.log(partSize);
    console.log(totalParts);
   
    const uploadPartsContainer = document.getElementById('{UploadPartsElement}');
    const uploadProgressContainer = document.getElementById('{UploadProgressElement}');
    uploadPartsContainer.innerHTML = ''; // Clear previous statuses

    // Create the main progress bar
    const progressBarContainer = document.createElement('div');
    const progressBar = document.createElement('progress');
    progressBar.max = totalParts; // Total number of parts
    progressBar.value = 0; // Initial progress is 0
    progressBar.style.width = '100%'; // Full width
    uploadProgressContainer.appendChild(progressBar);

    const uploadPromises = [];
    const statusElements = new Map(); // Store references to status elements
    let completedParts = 0; // Track completed parts

    for (let i = 0; i < totalParts; i++) {{
        const start = i * partSize;
        const end = Math.min(start + partSize, file.size);
        const blob = file.slice(start, end);

        const partForm = new FormData();
        partForm.append(""{Action}"", ""{UploadPartAction}"");
        partForm.append(""{FilePartSequence}"", i);
        partForm.append(""{FilePartData}"", blob);

         // Create status element
            const statusElement = document.createElement('div');
            statusElement.id = `part-${{i}}`;
            statusElement.textContent = `Part ${{i+1}}: Uploading...`;
            uploadPartsContainer.appendChild(statusElement);
            statusElements.set(i, statusElement);

        // Upload part and update status
        const uploadPromise = fetch('/', {{
            method: 'POST',
            body: partForm
        }}).then(response => {{
            if (response.ok) {{
                statusElement.textContent = `Part ${{i + 1}}: Uploaded ✅`;
                uploadPartsContainer.prepend(statusElement);
                completedParts++;
                progressBar.value = completedParts;
                setTimeout(() => {{ // Remove after 1 second
                    statusElement.remove();
                    statusElements.delete(i);
                }}, 1000);
            }} else {{
                statusElement.textContent = `Part ${{i + 1}}: Failed ❌`;
            }}
        }}).catch(() => {{
            statusElement.textContent = `Part ${{i + 1}}: Failed ❌`;
        }});

        uploadPromises.push(uploadPromise);
    }}

    // Function to move completed items to the top
    function moveToTop(element) {{
        uploadPartsContainer.prepend(element);
    }}

    const results = await Promise.all(uploadPromises);
    const completeForm = document.createElement('form');
    completeForm.method = 'POST';
    completeForm.enctype = 'multipart/form-data';

    const completeFormAction = document.createElement('input');
    completeFormAction.type = 'hidden';
    completeFormAction.name = ""{Action}"";
    completeFormAction.value = ""{CompleteUploadAction}"";
    completeForm.appendChild(completeFormAction);
    document.body.appendChild(completeForm);
    completeForm.submit();
}});
" : string.Empty)}


";
}

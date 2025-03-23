using System.Data.SQLite;
using System.Text;
using System.Text.Json;
using DotNetFlix.Data;
using Microsoft.AspNetCore.Http;

namespace DotNetFlix.Pages;

internal class Upload : Page
{
    private const string FileUploadIdKey = "FileUploadId";
    const string UploadFileNameKey = "UploadFileName";

    const int UploadPartSize = 1024 * 1024 * 5;

    const string FileInputElement = "File";
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
    const string ConfirmAction = "Confirm";

    enum ViewMode
    {
        Default,
        UploadComplete
    }

    public override async Task Get(HttpContext context, SQLiteConnection sql, long sessionId)
    {
        var session = sql.GetSession(sessionId);
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
        <h1>Uploaded File Information</h1>
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
        <h1>Transcoding settings</h1>
        <b>FFMPEG - Constant Rate Factor (CRF)</b>
        <input type='text' value='18'>
        <h2>Preview</h2>
        <p>Generate a 30 second preview that demonstrates the selected FFMPEG arguments.</p>
        <b>Start Time (seconds)</b>
        <input type='text' value='0'>
        <button type='submit' name='{Action}' value='{GeneratePreviewAction}'>Generate Preview</button>       
        <br>
        <video src='/'></video>
        <br>
        <h2>Confirmation</h2>
        <p>After clicking <b>Confirm</b>, the ...
        <button type='submit' name='{Action}' value='{ConfirmAction}'>Confirm</button>       
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

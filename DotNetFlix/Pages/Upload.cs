using System.Text;
using System.Text.Json;
using DotNetFlix.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace DotNetFlix.Pages;

internal class Upload : Page
{
    const string UploadFileNameKey = "UploadFileName";

    const int UploadPartSize = 1024 * 1024 * 5;

    const string FileInputElement = "File";
    const string FileName = "FileName";
    const string FileSize = "FileSize";
    const string FilePartSequence = "FilePartSequence";
    const string FilePartData = "FilePartData";
    const string UploadButtonElement = "UploadButton";
    const string CancelAction = "Cancel";
    const string BeginUploadAction = "BeginUpload";
    const string UploadPartAction = "UploadPart";
    const string CompleteUploadAction = "CompleteUpload";

    public override async Task Get(HttpContext context, SqliteConnection sql, long sessionId)
    {
        var session = sql.GetSession(sessionId);
        var view = HtmlTemplate(Html(), Css(), Js());
        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(view), context.RequestAborted);
    }

    public override async Task Post(HttpContext context, SqliteConnection sql, long sessionId)
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

                    var uploadFileName = Guid.NewGuid().ToString("N") + new FileInfo(fileName).Extension;
                    sql.SetSessionData(sessionId, UploadFileNameKey, uploadFileName);
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

                    var uploadFileName = sql.GetSessionData(sessionId, UploadFileNameKey);
                    using var fileStream = new FileStream(uploadFileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write, 4096, true);
                    fileStream.Seek(filePartSequence * UploadPartSize, SeekOrigin.Begin);
                    await file.CopyToAsync(fileStream);
                    Console.WriteLine(filePartSequence);
                    break;
                }
            case CompleteUploadAction:
                {
                    await Get(context, sql, sessionId);
                    break;
                }
            case CancelAction:
                {
                    sql.SetSessionPage(sessionId, nameof(Home));
                    await Instance(nameof(Home)).Get(context, sql, sessionId);
                    break;
                }
            default:
                throw new NotImplementedException();
        }
    }

    string Html() => $@"
<form method=""POST"" enctype=""multipart/form-data"">
    <button type=""submit"" name=""{Action}"" value=""{CancelAction}"">Cancel</button>
</form>

<label for=""{FileInputElement}"">File:</label>
<input type=""file"" id=""{FileInputElement}"">
<br>
<button id=""{UploadButtonElement}"">Upload</button>";

    string Css() => $@"
form {{
    max-width: 400px;
    margin: 20px auto;
    padding: 20px;
    background: #f8f9fa;
    border-radius: 10px;
    box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
    font-family: Arial, sans-serif;
    box-sizing: border-box;
}}

label {{
    font-weight: bold;
    display: block;
    margin: 10px 0 5px;
}}

select,
input[type=""text""],
input[type=""file""],
button {{
    width: 100%;
    padding: 10px;
    margin-top: 5px;
    border: 1px solid #ccc;
    border-radius: 5px;
    box-sizing: border-box; /* Prevents overflow */
}}

button {{
    background: #007bff;
    color: white;
    font-weight: bold;
    border: none;
    cursor: pointer;
    margin-top: 15px;
    transition: background 0.2s;
}}

button:hover {{
    background: #0056b3;
}}
";
    string Js() => $@"
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
    
    // Step 2: Upload parts in parallel
    const uploadPromises = [];
    for (let i = 0; i < totalParts; i++) {{
        const start = i * partSize;
        const end = Math.min(start + partSize, file.size);
        const blob = file.slice(start, end);

        const partForm = new FormData();
        partForm.append(""{Action}"", ""{UploadPartAction}"");
        partForm.append(""{FilePartSequence}"", i);
        partForm.append(""{FilePartData}"", blob);

        uploadPromises.push(fetch('/', {{
            method: 'POST',
            body: partForm
        }}));
    }}

    const results = await Promise.all(uploadPromises);

    if (results.some(res => !res.ok)) {{
        alert(""Some parts failed to upload."");
        return;
    }}


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

";
}

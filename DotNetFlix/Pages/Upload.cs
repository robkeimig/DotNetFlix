using DotNetFlix.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace DotNetFlix.Pages;

internal class Upload : Page
{
    const string TypeElement = "Type";
    const string TitleElement = "Title";
    const string FileElement = "File";
    const string CancelAction = "Cancel";
    const string UploadAction = "Upload";
    

    public override async Task<string> Get(SqliteConnection sql, long sessionId)
    {
        return HtmlTemplate(Html(), Css(), Js());
    }

    public override async Task Post(SqliteConnection sql, long sessionId, IFormCollection form)
    {
        switch (form[Action])
        {
            case CancelAction:
                sql.SetSessionPage(sessionId, nameof(Home));
                break;
            case UploadAction:
                var title = form[TitleElement];
                var file = form.Files[FileElement] ?? throw new Exception();
                var fileExtension = new FileInfo(file.FileName).Extension;
                var uploadedFileName = Guid.NewGuid().ToString("N") + fileExtension;
                
                await using (var stream = new FileStream(uploadedFileName, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await file.CopyToAsync(stream);
                }

                sql.CreateMedia(uploadedFileName, title);
                sql.SetSessionPage(sessionId, nameof(Home));
                break;
        }
    }

    string Html() => $@"
<form method=""POST"" enctype=""multipart/form-data"">
    <button type=""submit"" name=""{Action}"" value=""{CancelAction}"">Cancel</button>
</form>

<form method=""POST"" enctype=""multipart/form-data"">
    <label for=""media-type"">Media Type:</label>
    <select id=""media-type"" name=""{TypeElement}"">
        <option value="""" disabled selected>Select Media Type</option>
        <option value=""movie"">Movie</option>
    </select>
    <br>
    <label for=""title"">Title:</label>
    <input type=""text"" id=""title"" name=""{TitleElement}"" required>
    <br>
    <label for=""file-upload"">File:</label>
    <input type=""file"" id=""file-upload"" name=""{FileElement}"" required>
    <br>
    <button type=""submit"" name=""{Action}"" value=""{UploadAction}"">Upload</button>
</form>";

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
    string Js() => $@"";
}

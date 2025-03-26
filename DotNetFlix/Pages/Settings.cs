using System.Data.SQLite;
using System.Text;
using DotNetFlix.Data;
using Microsoft.AspNetCore.Http;

namespace DotNetFlix.Pages;

internal class Settings : Page
{
    const string HomeAction = "Home";
    const string SettingName = "SettingName";
    const string SettingValue = "SettingValue";
    const string ConfirmAction = "Confirm";
    const string CancelAction = "Cancel";

    enum ViewMode
    {
        None,
        EditingSetting
    }

    public override async Task Get(HttpContext context, SQLiteConnection sql, long sessionId)
    {
        var session = sql.GetSession(sessionId);
        _ = Enum.TryParse(sql.GetSessionData(sessionId, nameof(ViewMode)), out ViewMode viewMode);
        var settingName =  sql.GetSessionData(sessionId, SettingName);
        var settings = sql.GetSettings();
        var settingValue = default(string);
        
        if (!string.IsNullOrWhiteSpace(settingName))
        {
            settingValue = sql.GetSetting(settingName).Value;
        }

        await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(HtmlTemplate(Html(viewMode, settings, settingName, settingValue), Css(), Js())), context.RequestAborted);
    }

    public override async Task Post(HttpContext context, SQLiteConnection sql, long sessionId)
    {
        var form = await context.Request.ReadFormAsync();
        
        switch (form[Action])
        {
            case HomeAction:
                sql.ClearSessionData(sessionId, SettingName);
                sql.SetSessionPage(sessionId, nameof(Home));
                await Instance(nameof(Home)).Get(context, sql, sessionId);
                break;
            case ConfirmAction:
                var settingName = sql.GetSessionData(sessionId, SettingName);
                sql.UpdateSetting(settingName, form[SettingValue]);
                sql.ClearSessionData(sessionId, SettingName);
                sql.SetSessionData(sessionId, nameof(ViewMode), ViewMode.None.ToString());
                await Instance(nameof(Settings)).Get(context, sql, sessionId);
                break;
            case CancelAction:
                sql.ClearSessionData(sessionId, SettingName);
                sql.SetSessionData(sessionId, nameof(ViewMode), ViewMode.None.ToString());
                await Instance(nameof(Settings)).Get(context, sql, sessionId);
                break;
            case nameof(Configuration.AwsS3AccessKey):
                {
                    sql.SetSessionData(sessionId, SettingName, nameof(Configuration.AwsS3AccessKey));
                    sql.SetSessionData(sessionId, nameof(ViewMode), ViewMode.EditingSetting.ToString());
                    await Instance(nameof(Settings)).Get(context, sql, sessionId);
                    break;
                }
            case nameof(Configuration.AwsS3SecretKey):
                {
                    sql.SetSessionData(sessionId, SettingName, nameof(Configuration.AwsS3SecretKey));
                    sql.SetSessionData(sessionId, nameof(ViewMode), ViewMode.EditingSetting.ToString());
                    await Instance(nameof(Settings)).Get(context, sql, sessionId);
                    break;
                }
            case nameof(Configuration.AwsS3BucketName):
                {
                    sql.SetSessionData(sessionId, SettingName, nameof(Configuration.AwsS3BucketName));
                    sql.SetSessionData(sessionId, nameof(ViewMode), ViewMode.EditingSetting.ToString());
                    await Instance(nameof(Settings)).Get(context, sql, sessionId);
                    break;
                }
            case nameof(Configuration.FfmpegPath):
                {
                    sql.SetSessionData(sessionId, SettingName, nameof(Configuration.FfmpegPath));
                    sql.SetSessionData(sessionId, nameof(ViewMode), ViewMode.EditingSetting.ToString());
                    await Instance(nameof(Settings)).Get(context, sql, sessionId);
                    break;
                }
            case nameof(Configuration.CacheSize):
                {
                    sql.SetSessionData(sessionId, SettingName, nameof(Configuration.CacheSize));
                    sql.SetSessionData(sessionId, nameof(ViewMode), ViewMode.EditingSetting.ToString());
                    await Instance(nameof(Settings)).Get(context, sql, sessionId);
                    break;
                }
            default:
                throw new NotImplementedException();
        }
    }


    string Html(ViewMode viewMode, List<Setting> settings, string? settingName, string? settingValue) => $@"
<div class='container' />
    <form action='/' method='POST' enctype='multipart/data'>
        {(viewMode == ViewMode.None ? $@"
            <button type='submit' name='{Action}' value='{HomeAction}'>Home</button>
            <h1>Settings</h1>
            <table>
                <tr> 
                    <th>Name</th>
                    <th>Value</th>
                    <th>Edit</th>
                </tr>
                {string.Join('\n', settings.Select(setting => $@"
                <tr>
                    <td>{setting.Key}</td>
                    <td>{setting.Value}</td>
                    <td><button name='{Action}' value='{setting.Key}'>Edit</button></td>
                </tr>
                "))}
            </table>
        " : string.Empty)}

        {(viewMode == ViewMode.EditingSetting ? $@"
        <button type='submit' name='{Action}' value='{CancelAction}'>Cancel</button>
        <p>Editing {settingName}</p>
        <input type='text' name='{SettingValue}' value='{settingValue}'>
        <button type='submit' name='{Action}' value='{ConfirmAction}'>Confirm</button>
        " : string.Empty)}
    </form>
</div>
";

    string Css() => @$"
.container {{
}}

.container p {{
    margin-top:0;
}}
";

    string Js() => $@"
//...
";

}

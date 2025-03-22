using System.Data.SQLite;
using System.Text.Json;
using DotNetFlix;
using DotNetFlix.Data;

if (!System.IO.File.Exists($"{nameof(DotNetFlix.SystemPassword)}.json"))
{
    Console.WriteLine($"{nameof(DotNetFlix.SystemPassword)}.json file missing! It needs to be in the same directory as the executable.");
    return;
}

var systemPasswordJson = System.IO.File.ReadAllText(nameof(SystemPassword)+".json");
var systemPassword = JsonSerializer.Deserialize<SystemPassword>(systemPasswordJson).Password;
var sql = new SQLiteConnection("Data Source = data.db");

sql.Open();
sql.EnsureSchema();
sql.InitializeSettings();
sql.InitializeCryptography(systemPassword);

var configuration = sql.GetConfiguration();
var webServer = new WebServer(sql);

while (true)
{
    Thread.Sleep(1);
}
using System.Data.SQLite;
using System.Text.Json;
using DotNetFlix;
using DotNetFlix.Data;

if (!File.Exists($"{nameof(SystemPassword)}.json"))
{
    Console.WriteLine($"{nameof(SystemPassword)}.json file missing! It needs to be in the same directory as the executable.");
    return;
}

var systemPasswordJson = File.ReadAllText(nameof(SystemPassword)+".json");
var systemPassword = JsonSerializer.Deserialize<SystemPassword>(systemPasswordJson).Password;
var sql = new SQLiteConnection("Data Source = data.db");

sql.Open();
sql.EnsureSchema();
sql.InitializeSettings(systemPassword);
var webServer = new WebServer(sql);

while (true)
{
    //Background maintenance work goes here.
    Thread.Sleep(1);
}
using System.Data.SQLite;
using DotNetFlix;
using DotNetFlix.Data;

Directory.CreateDirectory(Constants.MediaBlockCachePath);
var sql = new SQLiteConnection("Data Source = data.db");
sql.Open();
sql.EnsureSchema();
sql.InitializeSettings();
var webServer = new WebServer(sql);

while (true)
{
    //Background maintenance work goes here.
    //throw new NotImplementedException("TODO: perform expiration of media block cache entries.");
    Thread.Sleep(1);
}
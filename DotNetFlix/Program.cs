using System.Data.SQLite;
using System.Diagnostics;
using DotNetFlix;
using DotNetFlix.Data;

Directory.CreateDirectory(Constants.MediaBlockCachePath);

var sql = new SQLiteConnection("Data Source = data.db");
sql.Open();
sql.EnsureSchema();
sql.InitializeSettings();

var webServer = new WebServer(sql);

var shrinkMediaBlockCacheStopWatch = new Stopwatch();
shrinkMediaBlockCacheStopWatch.Start();

while (true)
{
    if (shrinkMediaBlockCacheStopWatch.Elapsed.TotalSeconds > 60)
    {
        shrinkMediaBlockCacheStopWatch.Restart();
        sql.ShrinkMediaBlockCache();
    }

    Thread.Sleep(1);
}
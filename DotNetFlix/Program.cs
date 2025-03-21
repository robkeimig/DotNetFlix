﻿using System.Text.Json;
using DotNetFlix;
using DotNetFlix.Data;
using Microsoft.Data.Sqlite;

if (!File.Exists($"{nameof(SystemPassword)}.json"))
{
    Console.WriteLine($"{nameof(SystemPassword)}.json file missing! It needs to be in the same directory as the executable.");
    return;
}

var systemPasswordJson = File.ReadAllText(nameof(SystemPassword)+".json");
var systemPassword = JsonSerializer.Deserialize<SystemPassword>(systemPasswordJson).Password;
var sql = new SqliteConnection("Data Source = data.db");

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
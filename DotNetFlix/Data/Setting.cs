using System.Security.Cryptography;
using Dapper;
using Microsoft.Data.Sqlite;

namespace DotNetFlix.Data;

public class Setting
{
    public string Key { get; set; }
    public string Value { get; set; }
}


public class SettingsTable
{
    public const string TableName = "Settings";
    public string Key { get; set; }
    public string Value { get; set; }
}

public static class SettingsExtensions
{
    public static void InitializeSettings(this SqliteConnection sql)
    {
        var properties = typeof(Configuration).GetProperties();

        foreach(var property in properties)
        {
            var exists = sql.ExecuteScalar<bool>($@"
                SELECT 1 FROM {SettingsTable.TableName}
                WHERE       [{nameof(SettingsTable.Key)}] = @{nameof(SettingsTable.Key)}", new
            {
                Key = property.Name
            });

            if (!exists)
            {
                sql.Execute($@"
                    INSERT INTO {SettingsTable.TableName}  
                    (
                        [{nameof(SettingsTable.Key)}],
                        [{nameof(SettingsTable.Value)}]
                    ) 
                    VALUES 
                    (
                        @{nameof(SettingsTable.Key)},
                        @{nameof(SettingsTable.Value)}
                    )", new
                {
                    Key = property.Name,
                    Value = string.Empty
                });
            }
        }
    }

    public static List<Setting> GetSettings(this SqliteConnection sql)
    {
        var settings = sql.Query<SettingsTable>($@"SELECT * FROM {SettingsTable.TableName}");
        return Map(settings);
    }

    public static Setting GetSetting(this SqliteConnection sql, string key)
    {
        throw new NotImplementedException();
    }

    public static void UpdateSetting(this SqliteConnection sql, string key, string value)
    {
        throw new NotImplementedException();
    }

    public static Setting Map(SettingsTable setting) => new Setting
    {
        Key = setting.Key,
        Value = setting.Value,
    };

    public static SettingsTable Map(Setting setting) => new SettingsTable
    {
        Key = setting.Key,
        Value = setting.Value,
    };

    public static List<Setting> Map(IEnumerable<SettingsTable> settings) => settings.Select(Map).ToList();
}
using System.Data.SQLite;
using Dapper;

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
    public static void InitializeSettings(this SQLiteConnection sql)
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
                    Value = DefaultSettingValue(property.Name)
                });
            }
        }
    }

    private static string? DefaultSettingValue(string name) => name switch
    {
        nameof(Configuration.CacheSize) => Constants.DefaultCacheSize.ToString(),
        _ => null,
    };

    public static List<Setting> GetSettings(this SQLiteConnection sql)
    {
        var settings = sql.Query<SettingsTable>($@"SELECT * FROM {SettingsTable.TableName}");
        return Map(settings);
    }

    public static Setting GetSetting(this SQLiteConnection sql, string key)
    {
        var result = sql.QueryFirst<SettingsTable>($@"
            SELECT [{nameof(SettingsTable.Value)}] 
            FROM {SettingsTable.TableName}
            WHERE [{nameof(SettingsTable.Key)}] = @{nameof(SettingsTable.Key)}", new
        {
            Key = key
        });

        return Map(result);
    }

    public static void UpdateSetting(this SQLiteConnection sql, string key, string value)
    {
        sql.Execute($@"
            UPDATE {SettingsTable.TableName}
            SET     [{nameof(SettingsTable.Value)}] = @{nameof(SettingsTable.Value)}
            WHERE   [{nameof(SettingsTable.Key)}] = @{nameof(SettingsTable.Key)}", new
        {
            Key = key,
            Value = value
        });
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
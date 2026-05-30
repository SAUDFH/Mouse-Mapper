using MouseMapper.Models;
using System.IO;
using System.Text.Json;

namespace MouseMapper.Services;

public class ConfigManager
{
    private readonly string _configDir;
    private string ConfigPath => Path.Combine(_configDir, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public ConfigManager(string configDir)
    {
        _configDir = configDir;
    }

    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var defaults = new AppConfig();
            Save(defaults);
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
        }
        catch (JsonException)
        {
            try { File.Delete(ConfigPath); } catch { }
            var defaults = new AppConfig();
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(_configDir);
        string json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}

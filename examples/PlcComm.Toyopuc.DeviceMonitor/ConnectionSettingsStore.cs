using System.Text.Json;

namespace PlcComm.Toyopuc.DeviceMonitor;

internal static class ConnectionSettingsStore
{
    private static readonly string SettingsPath = Path.Combine(AppContext.BaseDirectory, "Toyopuc.DeviceMonitor.settings.json");
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    public static ConnectionSettingsDraft? Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return null;
            }

            var json = File.ReadAllText(SettingsPath);
            var draft = JsonSerializer.Deserialize<ConnectionSettingsDraft>(json);
            if (draft is null)
            {
                return null;
            }

            if (draft.Program is null || draft.Device is null || draft.StartAddress is null)
            {
                return null;
            }

            return draft;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(ConnectionSettingsDraft draft)
    {
        var json = JsonSerializer.Serialize(draft, SerializerOptions);
        File.WriteAllText(SettingsPath, json);
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                File.Delete(SettingsPath);
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }
}

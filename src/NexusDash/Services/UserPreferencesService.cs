using NexusDash.Models;
using System;
using System.IO;
using System.Text.Json;

namespace NexusDash.Services
{
    public static class UserPreferencesService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true
        };

        private static string PreferencesPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NexusDash",
            "settings.json");

        public static UserPreferences Load()
        {
            try
            {
                if (!File.Exists(PreferencesPath))
                {
                    return new UserPreferences();
                }

                var json = File.ReadAllText(PreferencesPath);
                return JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
            }
            catch
            {
                return new UserPreferences();
            }
        }

        public static void Update(Action<UserPreferences> update)
        {
            var preferences = Load();
            update(preferences);
            Save(preferences);
        }

        private static void Save(UserPreferences preferences)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PreferencesPath)!);
            File.WriteAllText(PreferencesPath, JsonSerializer.Serialize(preferences, SerializerOptions));
        }
    }
}

using NexusDash.Models;
using System;
using System.IO;
using System.Text.Json;

namespace NexusDash.Services
{
    public interface IUserPreferencesService
    {
        UserPreferences Load();
        void Update(Action<UserPreferences> update);
    }

    public sealed class UserPreferencesService : IUserPreferencesService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true
        };

        private readonly string _preferencesPath;

        public UserPreferencesService()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NexusDash",
                "settings.json"))
        {
        }

        internal UserPreferencesService(string preferencesPath)
        {
            _preferencesPath = preferencesPath;
        }

        public UserPreferences Load()
        {
            try
            {
                if (!File.Exists(_preferencesPath))
                {
                    return new UserPreferences();
                }

                var json = File.ReadAllText(_preferencesPath);
                return JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
            }
            catch
            {
                return new UserPreferences();
            }
        }

        public void Update(Action<UserPreferences> update)
        {
            var preferences = Load();
            update(preferences);
            Save(preferences);
        }

        private void Save(UserPreferences preferences)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_preferencesPath)!);
            File.WriteAllText(_preferencesPath, JsonSerializer.Serialize(preferences, SerializerOptions));
        }
    }
}

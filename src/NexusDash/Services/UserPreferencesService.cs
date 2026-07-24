using NexusDash.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NexusDash.Services
{
    public interface IUserPreferencesService
    {
        UserPreferences Load();
        void Update(Action<UserPreferences> update);
    }

    public sealed class UserPreferencesService : IUserPreferencesService
    {
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
                return JsonSerializer.Deserialize(json, UserPreferencesJsonSerializerContext.Default.UserPreferences)
                    ?? new UserPreferences();
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
            try
            {
                Save(preferences);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Preferences are non-critical and must not terminate the application.
            }
        }

        private void Save(UserPreferences preferences)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_preferencesPath)!);
            File.WriteAllText(
                _preferencesPath,
                JsonSerializer.Serialize(preferences, UserPreferencesJsonSerializerContext.Default.UserPreferences));
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(UserPreferences))]
    internal sealed partial class UserPreferencesJsonSerializerContext : JsonSerializerContext
    {
    }
}

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
            : this(ResolveDefaultPreferencesPath())
        {
        }

        /// <summary>旧版偏好在 Roaming（%APPDATA% 下），首次升级一次性迁到 Local（应用数据标准位置），旧文件保留作备份。</summary>
        private static string ResolveDefaultPreferencesPath()
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NexusDash",
                "settings.json");
            try
            {
                var legacy = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "NexusDash",
                    "settings.json");
                if (File.Exists(legacy) && !File.Exists(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.Copy(legacy, path, overwrite: false);
                }
            }
            catch
            {
                // 迁移失败不阻塞启动，按默认偏好处理
            }

            return path;
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

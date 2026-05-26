using System.Collections.Generic;

namespace NexusDash.Models
{
    public sealed class UserPreferences
    {
        public bool IsDarkTheme { get; set; } = true;
        public string CultureName { get; set; } = "zh-CN";
        public double WindowWidth { get; set; } = 1440;
        public double WindowHeight { get; set; } = 760;
        public Dictionary<string, bool> ProcessColumnVisibility { get; set; } = new();
    }
}

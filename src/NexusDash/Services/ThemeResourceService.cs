using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using NexusDash.Models;
using Semi.Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NexusDash.Services
{
    public sealed class ThemeResourceService : IThemeResourceService
    {
        public const string SystemThemeKey = "system";
        public const string LightThemeKey = "light";
        public const string DarkThemeKey = "dark";
        public const string AquaticThemeKey = "aquatic";
        public const string DesertThemeKey = "desert";
        public const string DuskThemeKey = "dusk";
        public const string NightSkyThemeKey = "night-sky";

        private static readonly ThemeOption[] Themes =
        [
            new(SystemThemeKey, "System", ThemeVariant.Default, "#4f8cff", true),
            new(LightThemeKey, "Light", ThemeVariant.Light, "#246ee8", false),
            new(DarkThemeKey, "Dark", ThemeVariant.Dark, "#4f8cff", true),
            new(AquaticThemeKey, "Aquatic", SemiTheme.Aquatic, "#0ea5b7", false),
            new(DesertThemeKey, "Desert", SemiTheme.Desert, "#c77b2a", false),
            new(DuskThemeKey, "Dusk", SemiTheme.Dusk, "#8a63d2", false),
            new(NightSkyThemeKey, "NightSky", SemiTheme.NightSky, "#7aa2ff", true)
        ];

        private static readonly IReadOnlyDictionary<string, Palette> Palettes =
            new Dictionary<string, Palette>(StringComparer.OrdinalIgnoreCase)
            {
                [SystemThemeKey] = Palette.Dark,
                [LightThemeKey] = Palette.Light,
                [DarkThemeKey] = Palette.Dark,
                [AquaticThemeKey] = Palette.Aquatic,
                [DesertThemeKey] = Palette.Desert,
                [DuskThemeKey] = Palette.Dusk,
                [NightSkyThemeKey] = Palette.NightSky
            };

        public IReadOnlyList<ThemeOption> GetThemeOptions()
        {
            return Themes;
        }

        public ThemeOption GetThemeOption(string? themeKey)
        {
            return Themes.FirstOrDefault(theme =>
                       string.Equals(theme.Key, themeKey, StringComparison.OrdinalIgnoreCase))
                   ?? Themes.First(theme => theme.Key == DarkThemeKey);
        }

        public void Apply(string themeKey)
        {
            if (Application.Current is not { } application)
            {
                return;
            }

            var theme = GetThemeOption(themeKey);
            application.RequestedThemeVariant = theme.ThemeVariant;
            ApplyPalette(application, ResolvePalette(application, theme));
        }

        public void Apply(bool isDarkTheme)
        {
            if (Application.Current is not { } application)
            {
                return;
            }

            var palette = isDarkTheme ? Palette.Dark : Palette.Light;
            ApplyPalette(application, palette);
            application.RequestedThemeVariant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        public static string ResolvePreferenceThemeKey(string? themeKey, bool fallbackIsDarkTheme)
        {
            var theme = Themes.FirstOrDefault(themeOption =>
                string.Equals(themeOption.Key, themeKey, StringComparison.OrdinalIgnoreCase));
            if (theme is not null)
            {
                return theme.Key;
            }

            return fallbackIsDarkTheme ? DarkThemeKey : LightThemeKey;
        }

        public static ThemeVariant ResolveThemeVariant(string? themeKey)
        {
            return Themes.FirstOrDefault(theme =>
                       string.Equals(theme.Key, themeKey, StringComparison.OrdinalIgnoreCase))
                       ?.ThemeVariant
                   ?? ThemeVariant.Dark;
        }

        private static void ApplyPalette(Application application, Palette palette)
        {
            SetBrush(application, nameof(palette.AppBackgroundBrush), palette.AppBackgroundBrush);
            SetBrush(application, nameof(palette.PanelBackgroundBrush), palette.PanelBackgroundBrush);
            SetBrush(application, nameof(palette.PanelAltBackgroundBrush), palette.PanelAltBackgroundBrush);
            SetBrush(application, nameof(palette.PanelBorderBrush), palette.PanelBorderBrush);
            SetBrush(application, nameof(palette.PrimaryTextBrush), palette.PrimaryTextBrush);
            SetBrush(application, nameof(palette.SecondaryTextBrush), palette.SecondaryTextBrush);
            SetBrush(application, nameof(palette.MutedTextBrush), palette.MutedTextBrush);
            SetBrush(application, nameof(palette.AccentBrush), palette.AccentBrush);
            SetBrush(application, nameof(palette.SuccessBrush), palette.SuccessBrush);
            SetBrush(application, nameof(palette.WarningBrush), palette.WarningBrush);
            SetBrush(application, nameof(palette.DangerBrush), palette.DangerBrush);
            SetBrush(application, nameof(palette.DialogMaskBrush), palette.DialogMaskBrush);
            SetBrush(application, nameof(palette.RowHoverBrush), palette.RowHoverBrush);
            SetBrush(application, nameof(palette.RowSelectedBrush), palette.RowSelectedBrush);
            SetBrush(application, nameof(palette.MarkdownTextBrush), palette.MarkdownTextBrush);
            SetBrush(application, nameof(palette.MarkdownMutedTextBrush), palette.MarkdownMutedTextBrush);
            SetBrush(application, nameof(palette.MarkdownBorderLineBrush), palette.MarkdownBorderLineBrush);
            SetBrush(application, nameof(palette.MarkdownAccentBrush), palette.MarkdownAccentBrush);
            SetBrush(application, nameof(palette.MarkdownQuoteBackgroundBrush), palette.MarkdownQuoteBackgroundBrush);
            SetBrush(application, nameof(palette.MarkdownCodeBackgroundBrush), palette.MarkdownCodeBackgroundBrush);
            SetBrush(application, nameof(palette.MarkdownInlineCodeBackgroundBrush), palette.MarkdownInlineCodeBackgroundBrush);
            SetBrush(application, nameof(palette.MarkdownTableHeaderBackgroundBrush), palette.MarkdownTableHeaderBackgroundBrush);
            SetBrush(application, nameof(palette.MarkdownAccentForegroundBrush), palette.MarkdownAccentForegroundBrush);
        }

        private static Palette ResolvePalette(Application application, ThemeOption theme)
        {
            if (string.Equals(theme.Key, SystemThemeKey, StringComparison.OrdinalIgnoreCase))
            {
                return application.ActualThemeVariant == ThemeVariant.Light
                    ? Palette.Light
                    : Palette.Dark;
            }

            return Palettes.TryGetValue(theme.Key, out var resolvedPalette)
                ? resolvedPalette
                : Palette.Dark;
        }

        private static void SetBrush(Application application, string key, string color)
        {
            application.Resources[key] = new SolidColorBrush(Color.Parse(color));
        }

        private sealed record Palette(
            string AppBackgroundBrush,
            string PanelBackgroundBrush,
            string PanelAltBackgroundBrush,
            string PanelBorderBrush,
            string PrimaryTextBrush,
            string SecondaryTextBrush,
            string MutedTextBrush,
            string AccentBrush,
            string SuccessBrush,
            string WarningBrush,
            string DangerBrush,
            string DialogMaskBrush,
            string RowHoverBrush,
            string RowSelectedBrush,
            string MarkdownTextBrush,
            string MarkdownMutedTextBrush,
            string MarkdownBorderLineBrush,
            string MarkdownAccentBrush,
            string MarkdownQuoteBackgroundBrush,
            string MarkdownCodeBackgroundBrush,
            string MarkdownInlineCodeBackgroundBrush,
            string MarkdownTableHeaderBackgroundBrush,
            string MarkdownAccentForegroundBrush)
        {
            public static Palette Dark { get; } = new(
                "#0d1320",
                "#151b26",
                "#1b2533",
                "#2b3443",
                "#e5e7eb",
                "#aab4c3",
                "#7d8999",
                "#4f8cff",
                "#38b277",
                "#d5a43a",
                "#e26363",
                "#99000000",
                "#263244",
                "#1d3a5f",
                "#e5e7eb",
                "#aab4c3",
                "#334155",
                "#60a5fa",
                "#1b2533",
                "#101827",
                "#1b2533",
                "#263244",
                "#0d1320");

            public static Palette Light { get; } = new(
                "#f4f7fb",
                "#ffffff",
                "#f2f4f7",
                "#e4e7ec",
                "#101828",
                "#667085",
                "#98a2b3",
                "#246ee8",
                "#21865a",
                "#a86f18",
                "#c63a3a",
                "#66000000",
                "#f2f4f7",
                "#e8f1ff",
                "#101828",
                "#667085",
                "#e4e7ec",
                "#1677ff",
                "#f7f8fa",
                "#f2f4f7",
                "#eef2f7",
                "#f2f4f7",
                "#ffffff");

            public static Palette Aquatic { get; } = new(
                "#f0fafd",
                "#ffffff",
                "#eff8fa",
                "#cde9ef",
                "#16323a",
                "#5c7880",
                "#7a99a1",
                "#0ea5b7",
                "#1f9d78",
                "#a87918",
                "#c84545",
                "#66000000",
                "#dff7fa",
                "#dff7fa",
                "#16323a",
                "#5c7880",
                "#cde9ef",
                "#0ea5b7",
                "#eff8fa",
                "#ecfbfe",
                "#e9f7fa",
                "#dff7fa",
                "#ffffff");

            public static Palette Desert { get; } = new(
                "#fcf7f0",
                "#ffffff",
                "#fff5e8",
                "#e8d9c2",
                "#32261b",
                "#7a6752",
                "#9a856d",
                "#c77b2a",
                "#4f8b5e",
                "#ad6f15",
                "#bd4b36",
                "#66000000",
                "#fff0d9",
                "#fff0d9",
                "#32261b",
                "#7a6752",
                "#e8d9c2",
                "#c77b2a",
                "#fff5e8",
                "#fff3e4",
                "#f7ead9",
                "#fff0d9",
                "#fffbf5");

            public static Palette Dusk { get; } = new(
                "#f5f7fb",
                "#ffffff",
                "#f3f1fa",
                "#ddd7ea",
                "#2c2237",
                "#72637f",
                "#8a7a98",
                "#8a63d2",
                "#3f9c73",
                "#a57922",
                "#bf4b5c",
                "#66000000",
                "#f0e9ff",
                "#f0e9ff",
                "#2c2237",
                "#72637f",
                "#ddd7ea",
                "#8a63d2",
                "#f3f1fa",
                "#f8f3ff",
                "#eee8f8",
                "#f0e9ff",
                "#ffffff");

            public static Palette NightSky { get; } = new(
                "#0d1320",
                "#172033",
                "#1e2a3f",
                "#2d3a52",
                "#ebf2ff",
                "#aab6c8",
                "#7f8ca3",
                "#7aa2ff",
                "#40b986",
                "#d6a645",
                "#ef6a73",
                "#99000000",
                "#1e2a3f",
                "#1e335a",
                "#ebf2ff",
                "#aab6c8",
                "#2d3a52",
                "#7aa2ff",
                "#172033",
                "#111827",
                "#1e2a3f",
                "#1e2a3f",
                "#0d1320");
        }
    }
}

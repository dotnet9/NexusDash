using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using CodeWF.Avalonia.Controls.Themes;
using NexusDash.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NexusDash.Services
{
    /// <summary>将应用原有资源名映射到共享主题，模式与配色分别持久化。</summary>
    public sealed class ThemeResourceService : IThemeResourceService
    {
        public const string SystemThemeKey = "system", LightThemeKey = "light", DarkThemeKey = "dark";
        public const string AquaticThemeKey = "aquatic", DesertThemeKey = "desert", DuskThemeKey = "dusk", NightSkyThemeKey = "night-sky";
        private readonly Application _application;
        private readonly StudioThemeController _controller;
        public ThemeResourceService()
        {
            _application = Application.Current ?? throw new InvalidOperationException("Application is not initialized.");
            _controller = App.ThemeController ?? throw new InvalidOperationException("Shared theme is not initialized.");
            _application.ActualThemeVariantChanged += OnThemeChanged;
            RefreshAliases();
        }
        public IReadOnlyList<ThemeOption> GetThemeOptions() => new[] { SystemThemeKey, LightThemeKey, DarkThemeKey }.Select(GetThemeOption).ToArray();
        public ThemeOption GetThemeOption(string? key)
        {
            var normalized = ResolvePreferenceThemeKey(key, true);
            var dark = normalized == DarkThemeKey || normalized == SystemThemeKey && IsEffectiveDark;
            return new(normalized, normalized, Brushes.Transparent, dark);
        }
        public IReadOnlyList<ThemeOption> GetPaletteOptions() => Enum.GetValues<StudioPalette>().Select(palette =>
        {
            _application.TryGetResource("StudioSwatch" + palette, _application.ActualThemeVariant, out var value);
            var brush = value as IBrush ?? Brushes.Transparent;
            return new ThemeOption(PaletteKey(palette), palette.ToString(), brush, IsEffectiveDark);
        }).ToArray();
        public void Apply(string key)
        {
            _controller.Mode = ResolveMode(key);
            RefreshAliases();
        }
        public void Apply(bool dark) => Apply(dark ? DarkThemeKey : LightThemeKey);
        public void ApplyPalette(string key)
        {
            _controller.Palette = ResolvePalette(key);
            RefreshAliases();
        }
        public bool IsEffectiveDark => _application.ActualThemeVariant == ThemeVariant.Dark || _application.ActualThemeVariant.InheritVariant == ThemeVariant.Dark;
        public static StudioThemeMode ResolveMode(string? key) => key switch
        {
            LightThemeKey => StudioThemeMode.Light,
            DarkThemeKey => StudioThemeMode.Dark,
            _ => StudioThemeMode.System
        };
        public static string ResolvePreferenceThemeKey(string? key, bool fallbackDark) => key switch
        {
            SystemThemeKey or LightThemeKey or DarkThemeKey => key,
            AquaticThemeKey or DesertThemeKey or DuskThemeKey => LightThemeKey,
            NightSkyThemeKey => DarkThemeKey,
            _ => fallbackDark ? DarkThemeKey : LightThemeKey
        };
        public static string ResolvePreferencePaletteKey(string? key, string? oldThemeKey) => PaletteKey(ResolvePalette(key ?? oldThemeKey));
        public static string PaletteKey(StudioPalette palette) => palette == StudioPalette.NightSky ? NightSkyThemeKey : palette.ToString().ToLowerInvariant();
        public static StudioPalette ResolvePalette(string? key) => Enum.TryParse<StudioPalette>(key?.Replace("-", ""), true, out var palette) && Enum.IsDefined(palette) ? palette : StudioPalette.DotNet;
        private void OnThemeChanged(object? sender, EventArgs args) => RefreshAliases();
        private void RefreshAliases()
        {
            foreach (var (alias, token) in Aliases)
                if (_application.TryGetResource(token, _application.ActualThemeVariant, out var value)) _application.Resources[alias] = value;
        }
        private static readonly (string Alias, string Token)[] Aliases =
        [
            ("AppBackgroundBrush", "StudioBackground"), ("PanelBackgroundBrush", "StudioSurface"),
            ("PanelAltBackgroundBrush", "StudioRaised"), ("PanelBorderBrush", "StudioBorder"),
            ("PrimaryTextBrush", "StudioText"), ("SecondaryTextBrush", "StudioMuted"),
            ("MutedTextBrush", "StudioMuted"), ("AccentBrush", "StudioAccent"),
            ("RowHoverBrush", "StudioRaised"), ("RowSelectedBrush", "StudioRaised"),
            ("MarkdownTextBrush", "StudioText"), ("MarkdownMutedTextBrush", "StudioMuted"),
            ("MarkdownBorderLineBrush", "StudioBorder"), ("MarkdownAccentBrush", "StudioAccent"),
            ("MarkdownQuoteBackgroundBrush", "StudioRaised"), ("MarkdownCodeBackgroundBrush", "StudioRaised"),
            ("MarkdownInlineCodeBackgroundBrush", "StudioRaised"), ("MarkdownTableHeaderBackgroundBrush", "StudioRaised"),
            ("MarkdownAccentForegroundBrush", "SemiColorPrimaryText")
        ];
        public void Dispose()
        {
            _application.ActualThemeVariantChanged -= OnThemeChanged;
            _controller.Dispose();
        }
    }
}

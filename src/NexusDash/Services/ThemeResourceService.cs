using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace NexusDash.Services
{
    public sealed class ThemeResourceService : IThemeResourceService
    {
        public void Apply(bool isDarkTheme)
        {
            if (Application.Current is not { } application)
            {
                return;
            }

            var palette = isDarkTheme ? Palette.Dark : Palette.Light;
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
            application.RequestedThemeVariant = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
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
            string RowSelectedBrush)
        {
            public static Palette Dark { get; } = new(
                "#101114",
                "#181a1f",
                "#20232a",
                "#31343d",
                "#f3f5f7",
                "#a3aab5",
                "#727a86",
                "#4f8cff",
                "#38b277",
                "#d5a43a",
                "#e26363",
                "#99000000",
                "#242933",
                "#263d5f");

            public static Palette Light { get; } = new(
                "#f4f6f8",
                "#ffffff",
                "#eef1f5",
                "#d7dce4",
                "#1d2430",
                "#5f6875",
                "#8b93a0",
                "#246ee8",
                "#21865a",
                "#a86f18",
                "#c63a3a",
                "#99000000",
                "#e9edf3",
                "#dceaff");
        }
    }
}

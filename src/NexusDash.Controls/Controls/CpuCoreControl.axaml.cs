using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NexusDash.Controls
{
    public partial class CpuCoreControl : UserControl
    {
        private const double ElevatedUsageThreshold = 50;
        private const double WarningUsageThreshold = 80;
        private const double WarningOverlayOpacity = 0.3;

        private static readonly IBrush NormalCoreBrush = new SolidColorBrush(Color.Parse("#007acc"));
        private static readonly IBrush NormalInnerRingBrush = new SolidColorBrush(Color.Parse("#4ec9b0"));
        private static readonly IBrush ElevatedBrush = new SolidColorBrush(Color.Parse("#d7ba7d"));
        private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#f14c4c"));

        public static readonly StyledProperty<double> CpuUsageProperty =
            AvaloniaProperty.Register<CpuCoreControl, double>(nameof(CpuUsage));

        public static readonly StyledProperty<double> CoreSizeProperty =
            AvaloniaProperty.Register<CpuCoreControl, double>(nameof(CoreSize), 180.0);

        public static readonly StyledProperty<double> InnerRingSizeProperty =
            AvaloniaProperty.Register<CpuCoreControl, double>(nameof(InnerRingSize), 140.0);

        public static readonly StyledProperty<IBrush> CoreColorProperty =
            AvaloniaProperty.Register<CpuCoreControl, IBrush>(nameof(CoreColor), NormalCoreBrush);

        public static readonly StyledProperty<IBrush> GlowColorProperty =
            AvaloniaProperty.Register<CpuCoreControl, IBrush>(nameof(GlowColor), NormalCoreBrush);

        public static readonly StyledProperty<IBrush> InnerRingColorProperty =
            AvaloniaProperty.Register<CpuCoreControl, IBrush>(nameof(InnerRingColor), NormalInnerRingBrush);

        public static readonly StyledProperty<IBrush> TextColorProperty =
            AvaloniaProperty.Register<CpuCoreControl, IBrush>(nameof(TextColor), Brushes.White);

        public static readonly StyledProperty<bool> IsWarningProperty =
            AvaloniaProperty.Register<CpuCoreControl, bool>(nameof(IsWarning));

        public static readonly StyledProperty<double> WarningOpacityProperty =
            AvaloniaProperty.Register<CpuCoreControl, double>(nameof(WarningOpacity));

        public double CpuUsage
        {
            get => GetValue(CpuUsageProperty);
            set => SetValue(CpuUsageProperty, value);
        }

        public double CoreSize
        {
            get => GetValue(CoreSizeProperty);
            set => SetValue(CoreSizeProperty, value);
        }

        public double InnerRingSize
        {
            get => GetValue(InnerRingSizeProperty);
            set => SetValue(InnerRingSizeProperty, value);
        }

        public IBrush CoreColor
        {
            get => GetValue(CoreColorProperty);
            set => SetValue(CoreColorProperty, value);
        }

        public IBrush GlowColor
        {
            get => GetValue(GlowColorProperty);
            set => SetValue(GlowColorProperty, value);
        }

        public IBrush InnerRingColor
        {
            get => GetValue(InnerRingColorProperty);
            set => SetValue(InnerRingColorProperty, value);
        }

        public IBrush TextColor
        {
            get => GetValue(TextColorProperty);
            set => SetValue(TextColorProperty, value);
        }

        public bool IsWarning
        {
            get => GetValue(IsWarningProperty);
            set => SetValue(IsWarningProperty, value);
        }

        public double WarningOpacity
        {
            get => GetValue(WarningOpacityProperty);
            set => SetValue(WarningOpacityProperty, value);
        }

        public CpuCoreControl()
        {
            InitializeComponent();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == CpuUsageProperty)
            {
                UpdateVisualState();
            }
        }

        private void UpdateVisualState()
        {
            if (CpuUsage < ElevatedUsageThreshold)
            {
                CoreColor = NormalCoreBrush;
                GlowColor = NormalCoreBrush;
                InnerRingColor = NormalInnerRingBrush;
                IsWarning = false;
                WarningOpacity = 0;
            }
            else if (CpuUsage < WarningUsageThreshold)
            {
                CoreColor = ElevatedBrush;
                GlowColor = ElevatedBrush;
                InnerRingColor = ElevatedBrush;
                IsWarning = false;
                WarningOpacity = 0;
            }
            else
            {
                CoreColor = WarningBrush;
                GlowColor = WarningBrush;
                InnerRingColor = WarningBrush;
                IsWarning = true;
                WarningOpacity = WarningOverlayOpacity;
            }
        }
    }
}

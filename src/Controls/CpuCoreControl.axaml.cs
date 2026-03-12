using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace NexusDash.Controls
{
    public partial class CpuCoreControl : UserControl
    {
        public static readonly StyledProperty<double> CpuUsageProperty =
            AvaloniaProperty.Register<CpuCoreControl, double>(nameof(CpuUsage), 0.0);

        public static readonly StyledProperty<double> RotationAngleProperty =
            AvaloniaProperty.Register<CpuCoreControl, double>(nameof(RotationAngle), 0.0);

        public static readonly StyledProperty<double> CoreSizeProperty =
            AvaloniaProperty.Register<CpuCoreControl, double>(nameof(CoreSize), 180.0);

        public static readonly StyledProperty<double> InnerRingSizeProperty =
            AvaloniaProperty.Register<CpuCoreControl, double>(nameof(InnerRingSize), 140.0);

        public static readonly StyledProperty<IBrush> CoreColorProperty =
            AvaloniaProperty.Register<CpuCoreControl, IBrush>(nameof(CoreColor), new SolidColorBrush(Color.Parse("#007acc")));

        public static readonly StyledProperty<IBrush> GlowColorProperty =
            AvaloniaProperty.Register<CpuCoreControl, IBrush>(nameof(GlowColor), new SolidColorBrush(Color.Parse("#007acc")));

        public static readonly StyledProperty<IBrush> InnerRingColorProperty =
            AvaloniaProperty.Register<CpuCoreControl, IBrush>(nameof(InnerRingColor), new SolidColorBrush(Color.Parse("#4ec9b0")));

        public static readonly StyledProperty<IBrush> SegmentColorProperty =
            AvaloniaProperty.Register<CpuCoreControl, IBrush>(nameof(SegmentColor), new SolidColorBrush(Color.Parse("#4ec9b0")));

        public static readonly StyledProperty<IBrush> TextColorProperty =
            AvaloniaProperty.Register<CpuCoreControl, IBrush>(nameof(TextColor), new SolidColorBrush(Color.Parse("#ffffff")));

        public static readonly StyledProperty<bool> IsWarningProperty =
            AvaloniaProperty.Register<CpuCoreControl, bool>(nameof(IsWarning), false);

        public static readonly StyledProperty<double> WarningOpacityProperty =
            AvaloniaProperty.Register<CpuCoreControl, double>(nameof(WarningOpacity), 0.0);

        public double CpuUsage
        {
            get => GetValue(CpuUsageProperty);
            set => SetValue(CpuUsageProperty, value);
        }

        public double RotationAngle
        {
            get => GetValue(RotationAngleProperty);
            set => SetValue(RotationAngleProperty, value);
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

        public IBrush SegmentColor
        {
            get => GetValue(SegmentColorProperty);
            set => SetValue(SegmentColorProperty, value);
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
            var usage = CpuUsage;

            // Update colors based on usage
            if (usage < 50)
            {
                CoreColor = new SolidColorBrush(Color.Parse("#007acc")); // Blue
                GlowColor = new SolidColorBrush(Color.Parse("#007acc"));
                InnerRingColor = new SolidColorBrush(Color.Parse("#4ec9b0")); // Cyan
                SegmentColor = new SolidColorBrush(Color.Parse("#4ec9b0"));
                IsWarning = false;
                WarningOpacity = 0.0;
            }
            else if (usage < 80)
            {
                CoreColor = new SolidColorBrush(Color.Parse("#d7ba7d")); // Amber
                GlowColor = new SolidColorBrush(Color.Parse("#d7ba7d"));
                InnerRingColor = new SolidColorBrush(Color.Parse("#d7ba7d"));
                SegmentColor = new SolidColorBrush(Color.Parse("#d7ba7d"));
                IsWarning = false;
                WarningOpacity = 0.0;
            }
            else
            {
                CoreColor = new SolidColorBrush(Color.Parse("#f14c4c")); // Red
                GlowColor = new SolidColorBrush(Color.Parse("#f14c4c"));
                InnerRingColor = new SolidColorBrush(Color.Parse("#f14c4c"));
                SegmentColor = new SolidColorBrush(Color.Parse("#f14c4c"));
                IsWarning = true;
                WarningOpacity = 0.3;
            }

            // Update rotation speed based on usage
            // Higher usage = faster rotation
            var targetSpeed = 0.5 + (usage / 100.0) * 3.0;
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace NexusDash.Views
{
    public sealed class MetricPlotView : Control
    {
        private const int Capacity = 60;
        private const double StrokeThickness = 2;

        public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
            AvaloniaProperty.Register<MetricPlotView, IReadOnlyList<double>?>(nameof(Values));

        public static readonly StyledProperty<IBrush?> StrokeProperty =
            AvaloniaProperty.Register<MetricPlotView, IBrush?>(nameof(Stroke));

        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<MetricPlotView, double>(nameof(Maximum), 100);

        static MetricPlotView()
        {
            AffectsRender<MetricPlotView>(ValuesProperty, StrokeProperty, MaximumProperty);
        }

        public IReadOnlyList<double>? Values
        {
            get => GetValue(ValuesProperty);
            set => SetValue(ValuesProperty, value);
        }

        public IBrush? Stroke
        {
            get => GetValue(StrokeProperty);
            set => SetValue(StrokeProperty, value);
        }

        public double Maximum
        {
            get => GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var values = Values;
            if (values is null || values.Count == 0)
            {
                return;
            }

            var plotBounds = new Rect(Bounds.Size).Deflate(StrokeThickness);
            if (plotBounds.Width <= 1 || plotBounds.Height <= 1)
            {
                return;
            }

            var maximum = Maximum > 0 && double.IsFinite(Maximum) ? Maximum : 100;
            var startIndex = Math.Max(values.Count - Capacity, 0);
            var visibleCount = values.Count - startIndex;
            if (visibleCount <= 0)
            {
                return;
            }

            var strokeBrush = ResolveStrokeBrush(Stroke);
            var pen = new Pen(strokeBrush, StrokeThickness);
            Point? previousPoint = null;
            for (var sourceIndex = startIndex; sourceIndex < values.Count; sourceIndex++)
            {
                var visibleIndex = sourceIndex - startIndex;
                var slotIndex = Capacity - visibleCount + visibleIndex;
                var value = values[sourceIndex];
                var normalizedValue = double.IsFinite(value)
                    ? Math.Clamp(value, 0, maximum) / maximum
                    : 0;
                var x = plotBounds.X + slotIndex / (double)(Capacity - 1) * plotBounds.Width;
                var y = plotBounds.Bottom - normalizedValue * plotBounds.Height;
                var point = new Point(x, y);

                if (previousPoint is { } previous)
                {
                    context.DrawLine(pen, previous, point);
                }
                else if (visibleCount == 1)
                {
                    context.DrawEllipse(strokeBrush, null, point, StrokeThickness, StrokeThickness);
                }

                previousPoint = point;
            }
        }

        private static IBrush ResolveStrokeBrush(IBrush? brush)
        {
            return brush is ISolidColorBrush
                ? brush
                : Brushes.DodgerBlue;
        }
    }
}

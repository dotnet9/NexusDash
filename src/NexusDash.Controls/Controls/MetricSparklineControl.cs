using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NexusDash.Controls
{
    public sealed class MetricSparklineControl : Control
    {
        private const double MinimumRenderableSize = 1;
        private static readonly Pen FramePen = new(new SolidColorBrush(Color.FromArgb(45, 120, 136, 156)), 1);

        public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
            AvaloniaProperty.Register<MetricSparklineControl, IReadOnlyList<double>?>(nameof(Values));

        public static readonly StyledProperty<IBrush?> StrokeProperty =
            AvaloniaProperty.Register<MetricSparklineControl, IBrush?>(nameof(Stroke), Brushes.DodgerBlue);

        public static readonly StyledProperty<double> StrokeThicknessProperty =
            AvaloniaProperty.Register<MetricSparklineControl, double>(nameof(StrokeThickness), 2);

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

        public double StrokeThickness
        {
            get => GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        static MetricSparklineControl()
        {
            AffectsRender<MetricSparklineControl>(ValuesProperty, StrokeProperty, StrokeThicknessProperty);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = new Rect(Bounds.Size);
            if (bounds.Width <= MinimumRenderableSize || bounds.Height <= MinimumRenderableSize)
            {
                return;
            }

            context.DrawRectangle(null, FramePen, bounds);

            var values = Values?.Where(static v => !double.IsNaN(v) && !double.IsInfinity(v)).ToArray();
            if (values is null || values.Length < 2 || Stroke is null)
            {
                return;
            }

            var max = Math.Max(values.Max(), 1);
            var min = Math.Min(values.Min(), 0);
            var range = Math.Max(max - min, 1);
            var geometry = new StreamGeometry();

            using (var ctx = geometry.Open())
            {
                for (var i = 0; i < values.Length; i++)
                {
                    var x = values.Length == 1 ? 0 : i * bounds.Width / (values.Length - 1);
                    var y = bounds.Height - ((values[i] - min) / range * bounds.Height);
                    var point = new Point(x, Math.Clamp(y, 0, bounds.Height));

                    if (i == 0)
                    {
                        ctx.BeginFigure(point, false);
                    }
                    else
                    {
                        ctx.LineTo(point);
                    }
                }
            }

            context.DrawGeometry(null, new Pen(Stroke, StrokeThickness), geometry);
        }
    }
}

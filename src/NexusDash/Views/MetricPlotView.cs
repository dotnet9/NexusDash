using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ScottPlot;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SkiaSharp;
using AvaloniaImage = Avalonia.Controls.Image;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;
using AvaloniaColor = Avalonia.Media.Color;
using ScottPlotColor = ScottPlot.Color;

namespace NexusDash.Views
{
    public sealed class MetricPlotView : UserControl
    {
        private const int Capacity = 60;
        private const int FallbackWidth = 260;
        private const int FallbackHeight = 64;
        private static readonly ScottPlotColor Transparent = ScottPlotColor.FromARGB(0);

        public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
            AvaloniaProperty.Register<MetricPlotView, IReadOnlyList<double>?>(nameof(Values));

        public static readonly StyledProperty<IBrush?> StrokeProperty =
            AvaloniaProperty.Register<MetricPlotView, IBrush?>(nameof(Stroke));

        public static readonly StyledProperty<double> MaximumProperty =
            AvaloniaProperty.Register<MetricPlotView, double>(nameof(Maximum), 100);

        private readonly AvaloniaImage _image;
        private readonly Plot _plot = new();

        static MetricPlotView()
        {
            ValuesProperty.Changed.AddClassHandler<MetricPlotView>((view, _) => view.UpdatePlot());
            StrokeProperty.Changed.AddClassHandler<MetricPlotView>((view, _) => view.UpdatePlot());
            MaximumProperty.Changed.AddClassHandler<MetricPlotView>((view, _) => view.UpdatePlot());
        }

        public MetricPlotView()
        {
            _image = new AvaloniaImage
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                IsHitTestVisible = false,
                Stretch = Stretch.Fill
            };

            Content = _image;
            ConfigurePlot();
            SizeChanged += (_, _) => UpdatePlot();
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

        private void ConfigurePlot()
        {
            _plot.HideAxesAndGrid();
            _plot.SetStyle(new PlotStyle
            {
                FigureBackgroundColor = Transparent,
                DataBackgroundColor = Transparent,
                AxisColor = Transparent,
                GridMajorLineColor = Transparent
            });
        }

        private void UpdatePlot()
        {
            var values = Values?
                .TakeLast(Capacity)
                .Select(static value => double.IsFinite(value) ? value : 0)
                .ToArray() ?? [];

            _plot.Clear();
            ConfigurePlot();

            if (values.Length > 0)
            {
                var offset = Capacity - values.Length;
                var xs = Enumerable.Range(0, values.Length)
                    .Select(index => (double)(offset + index))
                    .ToArray();
                var scatter = _plot.Add.ScatterLine(xs, values, ToScottPlotColor(Stroke));
                scatter.LineWidth = 2;
                scatter.MarkerSize = 0;
            }

            var maximum = Maximum > 0 && double.IsFinite(Maximum) ? Maximum : 100;
            _plot.Axes.SetLimits(0, Capacity - 1, 0, maximum);
            RenderImage();
        }

        private void RenderImage()
        {
            var width = Bounds.Width > 1 ? (int)System.Math.Ceiling(Bounds.Width) : FallbackWidth;
            var height = Bounds.Height > 1 ? (int)System.Math.Ceiling(Bounds.Height) : FallbackHeight;

            using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Transparent);
            _plot.Render(surface.Canvas, width, height);

            using var snapshot = surface.Snapshot();
            using var data = snapshot.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(data.ToArray());
            var bitmap = new AvaloniaBitmap(stream);
            var previous = _image.Source as System.IDisposable;
            _image.Source = bitmap;
            previous?.Dispose();
        }

        private static ScottPlotColor ToScottPlotColor(IBrush? brush)
        {
            return brush is ISolidColorBrush solid
                ? ToScottPlotColor(solid.Color)
                : ScottPlotColor.FromHex("#1A73E8");
        }

        private static ScottPlotColor ToScottPlotColor(AvaloniaColor color)
        {
            var argb =
                ((uint)color.A << 24) |
                ((uint)color.R << 16) |
                ((uint)color.G << 8) |
                color.B;
            return ScottPlotColor.FromARGB(argb);
        }
    }
}

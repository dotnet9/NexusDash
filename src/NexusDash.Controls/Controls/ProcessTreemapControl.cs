using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using NexusDash.Controls.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NexusDash.Controls
{
    public sealed class ProcessTreemapControl : Control
    {
        private const int MaxRenderedItems = 32;
        private const double OuterPadding = 6;
        private const double TileGap = 2;
        private const double TileCornerRadius = 3;
        private const double LabelMinWidth = 72;
        private const double LabelMinHeight = 34;
        private const double LabelFontSize = 11;
        private const int MaxLabelLength = 42;
        private const int TrimmedLabelLength = 39;

        private static readonly SolidColorBrush EmptyBackgroundBrush = new(Color.FromArgb(25, 127, 146, 173));
        private static readonly Pen TileBorderPen = new(new SolidColorBrush(Color.FromArgb(130, 255, 255, 255)), 1);
        private static readonly Color[] TilePalette =
        [
            Color.Parse("#2f8cff"),
            Color.Parse("#3bb273"),
            Color.Parse("#d99a2b"),
            Color.Parse("#7b61ff"),
            Color.Parse("#e25555"),
            Color.Parse("#16a3a3"),
            Color.Parse("#c76dd8"),
            Color.Parse("#607d8b")
        ];

        public static readonly StyledProperty<IReadOnlyList<TreemapItem>?> ItemsProperty =
            AvaloniaProperty.Register<ProcessTreemapControl, IReadOnlyList<TreemapItem>?>(nameof(Items));

        public IReadOnlyList<TreemapItem>? Items
        {
            get => GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        static ProcessTreemapControl()
        {
            AffectsRender<ProcessTreemapControl>(ItemsProperty);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var bounds = new Rect(Bounds.Size);
            context.DrawRectangle(EmptyBackgroundBrush, null, bounds);

            var items = Items?
                .Where(static item => item.Weight > 0)
                .OrderByDescending(static item => item.Weight)
                .Take(MaxRenderedItems)
                .ToArray();

            if (items is null || items.Length == 0)
            {
                return;
            }

            var total = items.Sum(static item => item.Weight);
            if (total <= 0)
            {
                return;
            }

            RenderSlice(context, bounds.Deflate(OuterPadding), items, total, vertical: bounds.Width > bounds.Height, level: 0);
        }

        private static void RenderSlice(
            DrawingContext context,
            Rect bounds,
            IReadOnlyList<TreemapItem> items,
            double total,
            bool vertical,
            int level)
        {
            if (items.Count == 0 || bounds.Width <= 1 || bounds.Height <= 1)
            {
                return;
            }

            var offset = 0d;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var ratio = item.Weight / total;
                var isLast = i == items.Count - 1;
                Rect rect;

                if (vertical)
                {
                    var width = isLast ? bounds.Width - offset : bounds.Width * ratio;
                    rect = new Rect(bounds.X + offset, bounds.Y, Math.Max(width, 0), bounds.Height);
                    offset += width;
                }
                else
                {
                    var height = isLast ? bounds.Height - offset : bounds.Height * ratio;
                    rect = new Rect(bounds.X, bounds.Y + offset, bounds.Width, Math.Max(height, 0));
                    offset += height;
                }

                rect = rect.Deflate(TileGap);
                if (rect.Width <= 1 || rect.Height <= 1)
                {
                    continue;
                }

                var color = PickColor(level + i);
                context.DrawRectangle(
                    new SolidColorBrush(color),
                    TileBorderPen,
                    rect,
                    TileCornerRadius,
                    TileCornerRadius);

                if (rect.Width > LabelMinWidth && rect.Height > LabelMinHeight)
                {
                    var text = $"{item.Label}  {item.ValueText}";
                    if (text.Length > MaxLabelLength)
                    {
                        text = text[..TrimmedLabelLength] + "...";
                    }

                    var formattedText = new FormattedText(
                        text,
                        CultureInfo.CurrentUICulture,
                        FlowDirection.LeftToRight,
                        Typeface.Default,
                        LabelFontSize,
                        Brushes.White)
                    {
                        MaxTextWidth = Math.Max(rect.Width - 10, 0),
                        MaxTextHeight = Math.Max(rect.Height - 8, 0)
                    };
                    context.DrawText(formattedText, new Point(rect.X + 5, rect.Y + 5));
                }
            }
        }

        private static Color PickColor(int index)
        {
            return TilePalette[index % TilePalette.Length];
        }
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Prism.Regions;
using System.Linq;
using System.Runtime.CompilerServices;

namespace NexusDash.Regions
{
    public sealed class SettingsTabControlRegionAdapter(IRegionBehaviorFactory regionBehaviorFactory)
        : RegionAdapterBase<TabControl>(regionBehaviorFactory)
    {
        private static readonly ConditionalWeakTable<TabItem, SelectionRegistration> SelectionRegistrations = new();

        protected override void Adapt(IRegion region, TabControl regionTarget)
        {
            void RefreshItems()
            {
                var selectedItem = regionTarget.SelectedItem;
                regionTarget.Items.Clear();
                foreach (var view in region.Views.OrderBy(GetOrder))
                {
                    regionTarget.Items.Add(view);
                    RegisterSelectionFallback(regionTarget, view);
                }

                if (selectedItem is not null && regionTarget.Items.Contains(selectedItem))
                {
                    regionTarget.SelectedItem = selectedItem;
                }
                else if (regionTarget.Items.Count > 0 && regionTarget.SelectedIndex < 0)
                {
                    regionTarget.SelectedIndex = 0;
                }
            }

            region.Views.CollectionChanged += (_, _) =>
            {
                RefreshItems();
            };

            RefreshItems();
        }

        protected override IRegion CreateRegion()
        {
            return new AllActiveRegion();
        }

        private static int GetOrder(object? view)
        {
            return view is Control { Tag: int order } ? order : int.MaxValue;
        }

        private static void RegisterSelectionFallback(TabControl tabControl, object? view)
        {
            if (view is not TabItem tabItem || SelectionRegistrations.TryGetValue(tabItem, out _))
            {
                return;
            }

            SelectionRegistrations.Add(tabItem, new SelectionRegistration());
            tabItem.AddHandler(
                InputElement.PointerPressedEvent,
                (_, _) => SelectTab(tabControl, tabItem),
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            tabItem.AddHandler(
                InputElement.PointerReleasedEvent,
                (_, _) => SelectTab(tabControl, tabItem),
                RoutingStrategies.Bubble,
                handledEventsToo: true);
        }

        private static void SelectTab(TabControl tabControl, TabItem tabItem)
        {
            var index = tabControl.Items.IndexOf(tabItem);
            if (index >= 0)
            {
                tabControl.SelectedIndex = index;
            }
        }

        private sealed class SelectionRegistration;
    }
}

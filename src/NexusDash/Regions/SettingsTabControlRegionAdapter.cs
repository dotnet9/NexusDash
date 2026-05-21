using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Prism.Regions;
using System.Linq;
using System.Runtime.CompilerServices;
using AtomTabControl = AtomUI.Desktop.Controls.TabControl;
using AtomTabItem = AtomUI.Desktop.Controls.TabItem;

namespace NexusDash.Regions
{
    public sealed class SettingsTabControlRegionAdapter(IRegionBehaviorFactory regionBehaviorFactory)
        : RegionAdapterBase<AtomTabControl>(regionBehaviorFactory)
    {
        private static readonly ConditionalWeakTable<AtomTabItem, SelectionRegistration> SelectionRegistrations = new();

        protected override void Adapt(IRegion region, AtomTabControl regionTarget)
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

        private static void RegisterSelectionFallback(AtomTabControl tabControl, object? view)
        {
            if (view is not AtomTabItem tabItem || SelectionRegistrations.TryGetValue(tabItem, out _))
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

        private static void SelectTab(AtomTabControl tabControl, AtomTabItem tabItem)
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

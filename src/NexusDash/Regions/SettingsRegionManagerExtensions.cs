using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using NexusDash.ViewModels.Settings;
using Prism.Ioc;
using Prism.Regions;
using AtomTabItem = AtomUI.Desktop.Controls.TabItem;

namespace NexusDash.Regions
{
    public static class SettingsRegionManagerExtensions
    {
        public static IRegionManager RegisterSettingsTab<TView>(
            this IRegionManager regionManager,
            IContainerProvider container,
            int order)
            where TView : Control
        {
            regionManager.RegisterViewWithRegion(RegionNames.SettingsRegion, () =>
            {
                var view = container.Resolve<TView>();
                var tabItem = new AtomTabItem
                {
                    Content = view,
                    DataContext = view,
                    Tag = order
                };

                tabItem.Bind(
                    HeaderedContentControl.HeaderProperty,
                    new Binding($"{nameof(Control.DataContext)}.{nameof(SettingsPageViewModelBase.Header)}")
                    {
                        Source = view
                    });

                return tabItem;
            });

            return regionManager;
        }
    }
}

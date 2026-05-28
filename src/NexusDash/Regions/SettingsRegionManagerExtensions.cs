using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using NexusDash.ViewModels.Settings;
using Prism.Ioc;
using Prism.Regions;

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
                var tabItem = new TabItem
                {
                    Content = view,
                    Tag = order
                };

                // 页签只承载设置页视图，标题从子视图的 ViewModel 读取，避免把 View 当作 DataContext 传播。
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

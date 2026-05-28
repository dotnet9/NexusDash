using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CodeWF.AvaloniaControls.Controls;
using Prism.Regions;
using System;
using System.Linq;

namespace NexusDash
{
    public partial class SettingsWindow : CodeWFWindow
    {
        private IRegionManager? _regionManager;
        private TabControl? _settingsRegionTabs;

        public SettingsWindow()
        {
            InitializeWindow(null);
        }

        public SettingsWindow(IRegionManager regionManager)
        {
            InitializeWindow(regionManager);
        }

        private void InitializeWindow(IRegionManager? regionManager)
        {
            _regionManager = regionManager;
            if (_regionManager is not null)
            {
                RegionManager.SetRegionManager(this, _regionManager);
            }

            AvaloniaXamlLoader.Load(this);
            _settingsRegionTabs = this.FindControl<TabControl>("SettingsRegionTabs");

            if (_regionManager is not null && _settingsRegionTabs is not null)
            {
                RegionManager.SetRegionManager(_settingsRegionTabs, _regionManager);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (var tabItem in _settingsRegionTabs?.Items.OfType<TabItem>() ?? [])
            {
                if (tabItem.Content is Control { DataContext: IDisposable viewModel })
                {
                    viewModel.Dispose();
                }
            }

            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }

            base.OnClosed(e);
        }
    }
}

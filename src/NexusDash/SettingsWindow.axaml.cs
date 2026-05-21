using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Prism.Regions;
using System;
using System.Linq;
using AtomTabControl = AtomUI.Desktop.Controls.TabControl;
using AtomTabItem = AtomUI.Desktop.Controls.TabItem;

namespace NexusDash
{
    public partial class SettingsWindow : AtomUI.Desktop.Controls.Window
    {
        private readonly IRegionManager? _regionManager;
        private AtomTabControl? _settingsRegionTabs;

        public SettingsWindow()
            : this(null)
        {
        }

        public SettingsWindow(IRegionManager? regionManager)
        {
            _regionManager = regionManager;
            if (_regionManager is not null)
            {
                RegionManager.SetRegionManager(this, _regionManager);
            }

            AvaloniaXamlLoader.Load(this);
            _settingsRegionTabs = this.FindControl<AtomTabControl>("SettingsRegionTabs");

            if (_regionManager is not null && _settingsRegionTabs is not null)
            {
                RegionManager.SetRegionManager(_settingsRegionTabs, _regionManager);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (var tabItem in _settingsRegionTabs?.Items.OfType<AtomTabItem>() ?? [])
            {
                if (tabItem.Content is Avalonia.Controls.Control { DataContext: IDisposable viewModel })
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

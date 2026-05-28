using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using NexusDash.ViewModels;

namespace NexusDash.Views
{
    public partial class TitleBarSearchAddOn : UserControl
    {
        public TitleBarSearchAddOn()
        {
            AvaloniaXamlLoader.Load(this);
            var searchLineEdit = this.FindControl<Control>("SearchLineEdit");
            if (searchLineEdit is not null)
            {
                searchLineEdit.KeyDown += SearchLineEdit_KeyDown;
            }
        }

        private void SearchLineEdit_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            (DataContext as MainWindowViewModel)?.ExecuteActiveSearch();
            e.Handled = true;
        }
    }
}

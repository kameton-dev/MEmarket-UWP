using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.ApplicationModel.Resources;
using MEmarket_UWP.Services;
using System.Threading.Tasks;

// Документацию по шаблону элемента "Диалоговое окно содержимого" см. по адресу https://go.microsoft.com/fwlink/?LinkId=234238

namespace MEmarket_UWP
{
    public sealed partial class RepoInputDialog : ContentDialog
    {
        private readonly DataService _dataService;
        private readonly ResourceLoader _loader = ResourceLoader.GetForCurrentView();

        public RepoInputDialog()
        {
            this.InitializeComponent();
            _dataService = DataService.GetInstance();
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            args.Cancel = true;
            var deferral = args.GetDeferral();

            try
            {
                ErrorTextBlock.Visibility = Visibility.Collapsed;

                var url = RepositoryUrlTextBox.Text.Trim();
                if (string.IsNullOrEmpty(url))
                {
                    ShowErrorMessage(_loader.GetString("RepoUrlInputMessage"));
                    return;
                }

                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    ShowErrorMessage(_loader.GetString("RepoUrlError"));
                    return;
                }

                try
                {
                    await _dataService.AddRepositoryAsync(url);
                    args.Cancel = false;
                }
                catch (Exception ex)
                {
                    ShowErrorMessage(_loader.GetString("ErrorText") + " " + ex.Message);
                }
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ShowErrorMessage(string message)
        {
            ErrorTextBlock.Text = message;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }
    }
}

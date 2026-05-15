using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using MEmarket_UWP.Services;
using MEmarket_UWP.Models;

namespace MEmarket_UWP
{
    public sealed partial class SearchPage : Page
    {
        private DataService _dataService;

        public SearchPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _dataService = DataService.GetInstance();
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await PerformSearch();
        }

        private async void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                await PerformSearch();
            }
        }

        private async void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            await PerformSearch();
        }

        private async Task PerformSearch()
        {
            var query = SearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                SearchResultsList.ItemsSource = null;
                NoResultsText.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                var results = await _dataService.SearchAppsAsync(query);
                SearchResultsList.ItemsSource = results;
                NoResultsText.Visibility = results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error searching: {ex.Message}");
            }
        }

        private void SearchResultsList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppItem app)
            {
                _dataService.SetCurrentApp(app);
                this.Frame.Navigate(typeof(AppPage));
            }
        }

        private void IconImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
        }
    }
}

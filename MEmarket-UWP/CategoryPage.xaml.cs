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
using MEmarket_UWP.DataModel;

namespace MEmarket_UWP
{
    public sealed partial class CategoryPage : Page
    {
        private DataService _dataService;
        private List<AppItem> _allApps;

        public CategoryPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is CategoryData category)
            {
                _dataService = DataService.GetInstance();

                try
                {
                    LoadingRing.IsActive = true;
                    _allApps = await _dataService.GetAppsByCategoryAsync(category.Key);
                    AppsListView.ItemsSource = _allApps;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading apps: {ex.Message}");
                }
                finally
                {
                    LoadingRing.IsActive = false;
                }
            }
        }

        private void AppsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppItem app)
            {
                _dataService.SetCurrentApp(app);
                this.Frame.Navigate(typeof(AppPage));
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchTextBox.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(query))
            {
                AppsListView.ItemsSource = _allApps;
            }
            else
            {
                var filteredApps = _allApps.Where(app => app.Name.ToLowerInvariant().Contains(query)).ToList();
                AppsListView.ItemsSource = filteredApps;
            }
        }

        private void IconImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
        }
    }
}

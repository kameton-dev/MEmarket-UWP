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
using MEmarket_UWP.DataModel;

namespace MEmarket_UWP
{
    public sealed partial class MainPage : Page
    {
        private DataService _dataService;

        public MainPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _dataService = DataService.GetInstance();
            
            try
            {
                LoadingRing.IsActive = true;
                var categories = await _dataService.GetCategoriesAsync();
                CategoriesListView.ItemsSource = categories;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading categories: {ex.Message}");
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }

        private void CategoriesListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is CategoryData category)
            {
                this.Frame.Navigate(typeof(CategoryPage), category);
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SearchPage));
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            this.Frame.Navigate(typeof(SettingsPage));
        }
    }
}


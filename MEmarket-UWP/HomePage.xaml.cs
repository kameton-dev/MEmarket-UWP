using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using MEmarket_UWP.Models;
using MEmarket_UWP.Services;
using MEmarket_UWP.DataModel;

namespace MEmarket_UWP
{
    public sealed partial class HomePage : Page
    {
        public ObservableCollection<CategoryData> Categories { get; set; } = new ObservableCollection<CategoryData>();
        public ObservableCollection<AppItem> NewApps { get; set; } = new ObservableCollection<AppItem>();

        private AppItem _featuredApp;

        public HomePage()
        {
            this.InitializeComponent();

            //CategoriesGridView.ItemsSource = Categories;
            //NewAppsGridView.ItemsSource = NewApps;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadHomeDataAsync();
        }

        // Загрузка и структурирование данных
        private async Task LoadHomeDataAsync()
        {
            LoadingRing.IsActive = true;
            FeaturedBanner.Visibility = Visibility.Collapsed;

            try
            {
                var dataService = DataService.GetInstance();
                
                await dataService.InitializeAsync();
                
                var appsList = await dataService.SearchAppsAsync("");

                if (appsList != null && appsList.Count > 0)
                {
                    Random rand = new Random();
                    
                    int randomIndex = rand.Next(appsList.Count);
                    
                    _featuredApp = appsList[randomIndex];

                    FeaturedAppName.Text = _featuredApp.Name;
                    FeaturedAppSummary.Text = !string.IsNullOrEmpty(_featuredApp.Summary)
                        ? _featuredApp.Summary
                        : _featuredApp.Description;

                    if (!string.IsNullOrEmpty(_featuredApp.Icon))
                    {
                        FeaturedAppIcon.Source = new BitmapImage(new Uri(_featuredApp.Icon));
                    }
                    FeaturedBanner.Visibility = Visibility.Visible;
                }
                
                RepoStatusText.Text = $"Подключено репозиториев: {dataService.Repositories.Count}. Последнее обновление: сегодня";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки домашней страницы: {ex.Message}");
                RepoStatusText.Text = "Ошибка подключения к сети Millennium Market";
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }

        // Клик баннер
        private void FeaturedBanner_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (_featuredApp != null)
            {
                var dataService = DataService.GetInstance();
                dataService.SetCurrentApp(_featuredApp);
                Frame.Navigate(typeof(AppPage));
            }
        }

        // Категории
        /* private void CategoriesGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var selectedCategory = e.ClickedItem as CategoryData;
            if (selectedCategory != null)
            {
                // TODO: Встройте вашу логику перехода на страницу категории
                // Например, если вы используете CategoryPage.xaml:
                // Frame.Navigate(typeof(CategoryPage), selectedCategory.Key);
            }
        } */

        // Приложения
        /*private void NewAppsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedApp = e.ClickedItem as AppItem;
            if (clickedApp != null)
            {
                var dataService = DataService.GetInstance();
                dataService.SetCurrentApp(clickedApp);
                Frame.Navigate(typeof(AppPage));
            }
        }*/
    }
}
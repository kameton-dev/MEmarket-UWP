using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
    public class HomeCategorySection
    {
        public string Name { get; set; }
        public ObservableCollection<AppItem> Apps { get; } = new ObservableCollection<AppItem>();
    }

    public sealed partial class HomePage : Page
    {
        public ObservableCollection<CategoryData> Categories { get; set; } = new ObservableCollection<CategoryData>();
        public ObservableCollection<AppItem> NewApps { get; set; } = new ObservableCollection<AppItem>();
        public ObservableCollection<HomeCategorySection> CategorySections { get; } = new ObservableCollection<HomeCategorySection>();
        private AppItem _featuredApp;
        private bool _featuredImageUsesFallback;
        private readonly Windows.ApplicationModel.Resources.ResourceLoader loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();

        public HomePage()
        {
            this.InitializeComponent();

            CategorySectionsList.ItemsSource = CategorySections;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await LoadHomeDataAsync();
        }

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

                    var bannerApps = appsList
                        .Where(app => !string.IsNullOrWhiteSpace(app.Banner))
                        .ToList();
                    var imageApps = appsList
                        .Where(app => !string.IsNullOrWhiteSpace(app.Banner) || !string.IsNullOrWhiteSpace(app.Icon))
                        .ToList();
                    var featuredApps = bannerApps.Count > 0
                        ? bannerApps
                        : imageApps;
                    if (featuredApps.Count > 0)
                    {
                        _featuredApp = featuredApps[rand.Next(featuredApps.Count)];

                        FeaturedAppName.Text = _featuredApp.Name;
                        _featuredImageUsesFallback = string.IsNullOrWhiteSpace(_featuredApp.Banner);
                        FeaturedAppImage.Stretch = _featuredImageUsesFallback
                            ? Windows.UI.Xaml.Media.Stretch.Uniform
                            : Windows.UI.Xaml.Media.Stretch.UniformToFill;
                        FeaturedAppImage.Source = new BitmapImage(new Uri(
                            _featuredImageUsesFallback ? _featuredApp.Icon : _featuredApp.Banner,
                            UriKind.Absolute));
                        FeaturedBanner.Visibility = Visibility.Visible;
                    }
                }

                RepoStatusText.Text = string.Format(loader.GetString("RepoStatusFormat"), dataService.Repositories.Count);
                await LoadCategorySectionsAsync(dataService);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки домашней страницы: {ex.Message}");
                RepoStatusText.Text = loader.GetString("RepoConnectionError");
            }
            finally
            {
                LoadingRing.IsActive = false;
            }
        }

        private void FeaturedAppImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (_featuredApp == null || _featuredImageUsesFallback || string.IsNullOrWhiteSpace(_featuredApp.Icon))
            {
                FeaturedBanner.Visibility = Visibility.Collapsed;
                return;
            }

            _featuredImageUsesFallback = true;
            FeaturedAppImage.Stretch = Windows.UI.Xaml.Media.Stretch.Uniform;
            FeaturedAppImage.Source = new BitmapImage(new Uri(_featuredApp.Icon, UriKind.Absolute));
        }

        private async Task LoadCategorySectionsAsync(DataService dataService)
        {
            CategorySections.Clear();

            var availableCategories = await dataService.GetCategoriesAsync();
            if (availableCategories == null || availableCategories.Count == 0)
                return;

            var random = new Random();
            var selectedCategories = availableCategories
                .OrderBy(category => random.Next())
                .Take(3)
                .ToList();

            foreach (var category in selectedCategories)
            {
                var apps = await dataService.GetAppsByCategoryAsync(category.Key);
                if (apps == null || apps.Count == 0)
                    continue;

                var section = new HomeCategorySection { Name = category.Name };
                foreach (var app in apps.Take(10))
                {
                    section.Apps.Add(app);
                }

                CategorySections.Add(section);
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

        private void CategoryApp_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is AppItem app)
            {
                var dataService = DataService.GetInstance();
                dataService.SetCurrentApp(app);
                Frame.Navigate(typeof(AppPage));
            }
        }
    }
}
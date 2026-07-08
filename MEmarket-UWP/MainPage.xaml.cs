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
using Windows.UI.Core;

namespace MEmarket_UWP
{
    public sealed partial class MainPage : Page
    {
        private DataService _dataService;

        public MainPage()
        {
            this.InitializeComponent();
            
            SystemNavigationListView.ItemsSource = new List<NavigationItem>
            {
                new NavigationItem { IconGlyph = "\uE11A", Name = "Поиск", TargetPageType = typeof(SearchPage) },
                new NavigationItem { IconGlyph = "\uE118", Name = "Загрузки и обновления", TargetPageType = typeof(InstalledAppPage) }
            };
            
            SettingsNavigationListView.ItemsSource = new List<NavigationItem>
            {
                new NavigationItem { IconGlyph = "\uE713", Name = "Настройки", TargetPageType = typeof(SettingsPage) }
            };
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            ContentFrame.Navigated += ContentFrame_Navigated;
            
            SystemNavigationManager.GetForCurrentView().BackRequested += MainPage_BackRequested;
            
            UpdateBackButtonVisibility();

            _dataService = DataService.GetInstance();
            
            try
            {
                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Visibility.Visible;
                var categories = await _dataService.GetCategoriesAsync();
                CategoriesNavigationListView.ItemsSource = categories;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading categories: {ex.Message}");
            }
            finally
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
            }
        }

        private void HamburgerButton_Click(object sender, RoutedEventArgs e)
        {
            RootSplitView.IsPaneOpen = !RootSplitView.IsPaneOpen;
        }

        private void CategoriesNavigationListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is CategoryData category)
            {
                SystemNavigationListView.SelectedItem = null;
                SettingsNavigationListView.SelectedItem = null;

                ContentFrame.Navigate(typeof(CategoryPage), category);
                RootSplitView.IsPaneOpen = false;
            }
        }

        private void SystemNavigationListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            CategoriesNavigationListView.SelectedItem = null;
            SettingsNavigationListView.SelectedItem = null;

            if (e.ClickedItem is NavigationItem item)
            {
                ContentFrame.Navigate(item.TargetPageType);
            }
            RootSplitView.IsPaneOpen = false;
        }

        private void SettingsNavigationListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            CategoriesNavigationListView.SelectedItem = null;
            SystemNavigationListView.SelectedItem = null;

            if (e.ClickedItem is NavigationItem item)
            {
                ContentFrame.Navigate(item.TargetPageType);
            }
            RootSplitView.IsPaneOpen = false;
        }

        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            UpdateBackButtonVisibility();
            if (e.SourcePageType == typeof(CategoryPage))
            {
                if (e.Parameter is CategoryData category)
                {
                    TitleTextBlock.Text = category.Name;
                }
                else
                {
                    TitleTextBlock.Text = "Категория";
                }
            }
            else if (e.SourcePageType == typeof(SearchPage))
            {
                TitleTextBlock.Text = "Поиск";
            }
            else if (e.SourcePageType == typeof(InstalledAppPage))
            {
                TitleTextBlock.Text = "Загрузки и обновления";
            }
            else if (e.SourcePageType == typeof(SettingsPage))
            {
                TitleTextBlock.Text = "Настройки";
            }
            else if (e.SourcePageType == typeof(RepoPropsPage))
            {
                if (e.Parameter is MEmarket_UWP.Models.Repository repo && !string.IsNullOrEmpty(repo.Name))
                {
                    TitleTextBlock.Text = repo.Name;
                }
                else
                {
                    TitleTextBlock.Text = "Репозиторий";
                }
            }
        }

        /* --- Эксперимент с кнопкой "назад" для десктопного варианта ---
         * TODO: решить судьбу этой задумки */

        private void UpdateBackButtonVisibility()
        {
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility =
                ContentFrame.CanGoBack ? AppViewBackButtonVisibility.Visible : AppViewBackButtonVisibility.Collapsed;
        }

        private void MainPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            if (ContentFrame.CanGoBack && !e.Handled)
            {
                e.Handled = true; 
                ContentFrame.GoBack();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            SystemNavigationManager.GetForCurrentView().BackRequested -= MainPage_BackRequested;
        }
    }

    public class NavigationItem
    {
        public string IconGlyph { get; set; }
        public string Name { get; set; }
        public Type TargetPageType { get; set; }
    }
}


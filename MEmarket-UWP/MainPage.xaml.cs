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
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using MEmarket_UWP.Models;
using Windows.Storage;
using Windows.ApplicationModel.Resources;   

namespace MEmarket_UWP
{
    public sealed partial class MainPage : Page
    {
        private DataService _dataService;

        public MainPage()
        {
            this.InitializeComponent();

            var resourceLoader = ResourceLoader.GetForCurrentView();
            string menuHome = resourceLoader.GetString("MenuHome");
            string menuSearch = resourceLoader.GetString("MenuSearch");
            string menuDownloads = resourceLoader.GetString("MenuDownloads");
            string menuSettings = resourceLoader.GetString("MenuSettings");

            SystemNavigationListView.ItemsSource = new List<NavigationItem>
            {
                new NavigationItem { IconGlyph = "\uE80F", Name = menuHome, TargetPageType = typeof(HomePage) },
                new NavigationItem { IconGlyph = "\uE11A", Name = menuSearch, TargetPageType = typeof(SearchPage) },
                new NavigationItem { IconGlyph = "\uE118", Name = menuDownloads, TargetPageType = typeof(InstalledAppPage) }
            };
            
            SettingsNavigationListView.ItemsSource = new List<NavigationItem>
            {
                new NavigationItem { IconGlyph = "\uE713", Name = menuSettings, TargetPageType = typeof(SettingsPage) }
            };
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _dataService = DataService.GetInstance();

            try
            {
                LoadingRing.IsActive = true;
                LoadingRing.Visibility = Visibility.Visible;
                await _dataService.InitializeAsync();
                var categories = await _dataService.GetCategoriesAsync();
                CategoriesNavigationListView.ItemsSource = categories;
                var localSettings = ApplicationData.Current.LocalSettings;
                bool isUpdateCheckEnabled = false;

                if (localSettings.Values.TryGetValue("BackgroundUpdateCheckEnabled", out object val) && val is bool boolValue)
                {
                    isUpdateCheckEnabled = boolValue;
                }
                
                if (isUpdateCheckEnabled)
                {
                    #pragma warning disable CS4014
                    Task.Run(async () => await CheckForUpdatesInBackgroundAsync());
                    #pragma warning restore CS4014
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки категорий: {ex.Message}");
            }
            finally
            {
                LoadingRing.IsActive = false;
                LoadingRing.Visibility = Visibility.Collapsed;
            }
            
            if (ContentFrame.Content == null)
            {
                ContentFrame.Navigate(typeof(HomePage));
                
                if (SystemNavigationListView != null)
                {
                    SystemNavigationListView.SelectedIndex = 0;
                }
            }
            
            ContentFrame.Navigated += ContentFrame_Navigated;
            SystemNavigationManager.GetForCurrentView().BackRequested += MainPage_BackRequested;
            UpdateBackButtonVisibility();
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
            if (e.SourcePageType == typeof(HomePage) ||
                e.SourcePageType == typeof(SearchPage) ||
                e.SourcePageType == typeof(InstalledAppPage) ||
                e.SourcePageType == typeof(SettingsPage))
            {
                ContentFrame.BackStack.Clear();
            }
            
            UpdateBackButtonVisibility();
            var resourceLoader = ResourceLoader.GetForCurrentView();

            if (e.SourcePageType == typeof(HomePage))
            {
                TitleTextBlock.Text = resourceLoader.GetString("MenuHome");
            }
            else if (e.SourcePageType == typeof(CategoryPage))
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
                TitleTextBlock.Text = resourceLoader.GetString("MenuSearch");
            }
            else if (e.SourcePageType == typeof(InstalledAppPage))
            {
                TitleTextBlock.Text = resourceLoader.GetString("MenuDownloads");
            }
            else if (e.SourcePageType == typeof(SettingsPage))
            {
                TitleTextBlock.Text = resourceLoader.GetString("MenuSettings");
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

        // --- МЕТОДЫ ФОНОВОЙ ПРОВЕРКИ И СИСТЕМНОГО ОПОВЕЩЕНИЯ ---

        // 1. Асинхронная фоновая проверка обновлений
        private async Task CheckForUpdatesInBackgroundAsync()
        {
            try
            {
                var localApps = await LocalAppsManager.LoadAppsAsync();
                if (localApps == null || localApps.Count == 0) return;

                var updates = await UpdateChecker.CheckForUpdatesAsync(localApps);

                UpdateChecker.CachedUpdates = updates ?? new List<UpdateableApp>();

                if (updates != null && updates.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Found {updates.Count} updates for background notification");
                    var dispatcher = CoreApplication.MainView?.CoreWindow?.Dispatcher;
                    if (dispatcher != null)
                    {
                        await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            foreach (var update in updates)
                            {
                                System.Diagnostics.Debug.WriteLine($"Showing toast for {update.Name} -> {update.NewVersion}");
                                ShowSystemToastNotification(update.Name, update.NewVersion);
                            }
                        });
                    }
                    else
                    {
                        foreach (var update in updates)
                        {
                            System.Diagnostics.Debug.WriteLine($"Showing toast for {update.Name} -> {update.NewVersion}");
                            ShowSystemToastNotification(update.Name, update.NewVersion);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка фоновой проверки обновлений: {ex.Message}");
            }
        }

        // 2. Создание и показ системного Toast-уведомления
        private void ShowSystemToastNotification(string appName, string newVersion)
        {
            try
            {
                var loader = ResourceLoader.GetForCurrentView();

                string titleText = loader.GetString("ToastUpdateTitle");
                string messageTemplate = loader.GetString("ToastUpdateMessageFormat");

                string rawMessage = string.Format(messageTemplate, appName, newVersion);

                string titleEscaped = EscapeXml(titleText);
                string messageEscaped = EscapeXml(rawMessage);

                string xml = $@"
                <toast>
                    <visual>
                        <binding template='ToastGeneric'>
                            <text>{titleEscaped}</text>
                            <text>{messageEscaped}</text>
                        </binding>
                    </visual>
                </toast>";

                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xml);

                var notifier = ToastNotificationManager.CreateToastNotifier();
                if (notifier == null)
                {
                    return;
                }

                ToastNotification toast = new ToastNotification(doc);

                toast.ExpirationTime = DateTimeOffset.UtcNow.AddDays(3);

                notifier.Show(toast);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка вывода уведомления: {ex.Message}");
            }
        }

        private string EscapeXml(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return input.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }

    public class NavigationItem
    {
        public string IconGlyph { get; set; }
        public string Name { get; set; }
        public Type TargetPageType { get; set; }
    }
}


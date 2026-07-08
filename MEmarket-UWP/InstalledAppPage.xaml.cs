using MEmarket_UWP.Models;
using MEmarket_UWP.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MEmarket_UWP
{
    public sealed partial class InstalledAppPage : Page
    {
        public ObservableCollection<TrackedApp> InstalledApps { get; set; } = new ObservableCollection<TrackedApp>();
        public ObservableCollection<UpdateableApp> AvailableUpdates { get; set; } = new ObservableCollection<UpdateableApp>();

        public InstalledAppPage()
        {
            this.InitializeComponent();
            
            InstalledListView.ItemsSource = InstalledApps;
            UpdatesListView.ItemsSource = AvailableUpdates;
        }
        
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            UpdatesListView.Visibility = Visibility.Collapsed;
            
            await RefreshInstalledListAsync();
        }
        
        private async Task RefreshInstalledListAsync()
        {
            LoadingRing.IsActive = true;
            InstalledApps.Clear();
            
            var localApps = await LocalAppsManager.LoadAppsAsync();
            foreach (var app in localApps)
            {
                InstalledApps.Add(app);
            }
            
            NoAppsTextBlock.Visibility = InstalledApps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LoadingRing.IsActive = false;
        }
        
        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            LoadingRing.IsActive = true;
            CheckUpdatesButton.IsEnabled = false;

            AvailableUpdates.Clear();
            
            var localApps = await LocalAppsManager.LoadAppsAsync();
            
            var updates = await UpdateChecker.CheckForUpdatesAsync(localApps);

            foreach (var update in updates)
            {
                AvailableUpdates.Add(update);
            }
            
            if (AvailableUpdates.Count > 0)
            {
                UpdatesListView.Visibility = Visibility.Visible;
            }
            else
            {
                UpdatesListView.Visibility = Visibility.Collapsed;
                
                var dialog = new ContentDialog
                {
                    Title = "(≧◡≦)",
                    Content = "Все ваши приложения обновлены до актуальной версии.",
                    CloseButtonText = "OK"
                };
                await dialog.ShowAsync();
            }

            CheckUpdatesButton.IsEnabled = true;
            LoadingRing.IsActive = false;
        }
        
        private async void SingleUpdate_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var appToUpdate = button?.DataContext as UpdateableApp;

            if (appToUpdate == null || string.IsNullOrEmpty(appToUpdate.AppUrl)) return;

            try
            {
                button.IsEnabled = false;
                LoadingRing.IsActive = true;
                
                string entryUrl = $"{appToUpdate.AppUrl.TrimEnd('/')}/entry.json";

                string cacheBuster = DateTime.Now.Ticks.ToString();
                entryUrl += "?v=" + cacheBuster;

                using (var client = new HttpClient())
                {
                    string entryJsonText = await client.GetStringAsync(new Uri(entryUrl));
                    string appFileName = null;
                    
                    if (JsonObject.TryParse(entryJsonText, out JsonObject entryRoot))
                    {
                        if (entryRoot.ContainsKey("versions"))
                        {
                            JsonArray versions = entryRoot.GetNamedArray("versions");
                            foreach (var v in versions)
                            {
                                var versionObj = v.GetObject();
                                string verStr = versionObj.GetNamedString("version");
                                
                                if (verStr == appToUpdate.NewVersion)
                                {
                                    appFileName = versionObj.GetNamedString("app_file");
                                    break;
                                }
                            }
                        }
                    }
                    
                    if (!string.IsNullOrEmpty(appFileName))
                    {
                        string downloadUrl = $"{appToUpdate.AppUrl.TrimEnd('/')}/{appFileName}";
                        var uri = new Uri(downloadUrl);
                        
                        var folder = ApplicationData.Current.LocalFolder;
                        var file = await folder.CreateFileAsync(appFileName, CreationCollisionOption.ReplaceExisting);
                        
                        var response = await client.GetAsync(uri);
                        response.EnsureSuccessStatusCode();

                        using (var httpStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = await file.OpenStreamForWriteAsync())
                        {
                            await httpStream.CopyToAsync(fileStream);
                        }
                        
                        var launchOptions = new LauncherOptions
                        {
                            DisplayApplicationPicker = false
                        };

                        var launched = await Launcher.LaunchFileAsync(file, launchOptions);
                        if (!launched)
                        {
                            var dialog = new ContentDialog
                            {
                                Title = "(_　_|||)",
                                Content = "Не удалось запустить файл установки пакета",
                                CloseButtonText = "ОК"
                            };
                            await dialog.ShowAsync();
                        }
                        else
                        {
                            try
                            {
                                await LocalAppsManager.RegisterInstalledAppAsync(
                                    appToUpdate.Id,
                                    appToUpdate.Name,
                                    appToUpdate.Icon,
                                    appToUpdate.NewVersion,
                                    appToUpdate.RepositoryUrl,
                                    appToUpdate.AppUrl
                                );

                                System.Diagnostics.Debug.WriteLine($"Приложение успешно обновлено в installed_apps.json: {appToUpdate.Name} (v{appToUpdate.NewVersion})");
                                
                                await RefreshInstalledListAsync();
                                
                                AvailableUpdates.Remove(appToUpdate);
                                if (AvailableUpdates.Count == 0)
                                {
                                    UpdatesListView.Visibility = Visibility.Collapsed;
                                }
                            }
                            catch (Exception dbEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"Ошибка installed_apps.json при обновлении версии: {dbEx.Message}");
                            }
                        }
                    }
                    else
                    {
                        var dialog = new ContentDialog
                        {
                            Title = "┐(￣ヘ￣;)┌",
                            Content = "Не удалось получить путь к файлу для этой версии в манифесте репозитория.",
                            CloseButtonText = "ОК"
                        };
                        await dialog.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "╮( ˘ ､ ˘ )╭",
                    Content = $"Не удалось загрузить обновление: {ex.Message}",
                    CloseButtonText = "ОК"
                };
                await dialog.ShowAsync();
            }
            finally
            {
                button.IsEnabled = true;
                LoadingRing.IsActive = false;
            }
        }

        private void AppsListView_ItemClick(object sender, ItemClickEventArgs e)
        {
        }

        private void IconImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            var image = sender as Image;
            if (image != null)
            {
                image.Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/StoreLogo.png"));
            }
        }

        // Посмотреть приложение
        private void ViewAppDetails_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuFlyoutItem;
            if (menuItem == null) return;
            
            var trackedApp = menuItem.DataContext as TrackedApp;
            if (trackedApp == null) return;
            
            string entryJsonUrl = trackedApp.AppUrl;
            if (!string.IsNullOrEmpty(entryJsonUrl))
            {
                entryJsonUrl = entryJsonUrl.EndsWith("/") ? entryJsonUrl + "entry.json" : entryJsonUrl + "/entry.json";
            }
            
            var appItem = new AppItem
            {
                Id = trackedApp.Id,
                Name = trackedApp.Name,
                Icon = trackedApp.Icon,
                AppUrl = trackedApp.AppUrl,
                EntryJsonUrl = entryJsonUrl
            };
            
            var dataService = Services.DataService.GetInstance();
            dataService.SetCurrentApp(appItem);
            
            Frame.Navigate(typeof(AppPage));
        }

        // "Забыть"
        private async void ForgetApp_Click(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as MenuFlyoutItem;
            if (menuItem == null) return;

            var appToForget = menuItem.DataContext as TrackedApp;
            if (appToForget == null) return;
            
            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "【・_・?】",
                Content = $"Вы действительно хотите удалить \"{appToForget.Name}\" из журнала?",
                PrimaryButtonText = "Удалить",
                CloseButtonText = "Отмена"
            };

            ContentDialogResult result = await confirmDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                LoadingRing.IsActive = true;

                try
                {
                    var apps = await LocalAppsManager.LoadAppsAsync();
                    
                    apps.RemoveAll(a => a.Id == appToForget.Id);
                    
                    await LocalAppsManager.SaveAppsAsync(apps);
                    
                    await RefreshInstalledListAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка при удалении: {ex.Message}");
                }
                finally
                {
                    LoadingRing.IsActive = false;
                }
            }
        }
    }
}
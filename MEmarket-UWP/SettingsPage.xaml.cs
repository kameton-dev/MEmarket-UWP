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
using Windows.Storage;

namespace MEmarket_UWP
{
    public sealed partial class SettingsPage : Page
    {
        private DataService _dataService;
        private bool _isInitializing;

        public SettingsPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _dataService = DataService.GetInstance();
            LoadRepositories();
            //LoadAndApplyTheme();
            LoadAppTypeSettings();
        }

        private void LoadRepositories()
        {
            RepositoriesListBox.ItemsSource = _dataService.Repositories;
        }

        private async void AddRepositoryButton_Click(object sender, RoutedEventArgs e)
        {
            var url = RepositoryUrlTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(url))
            {
                await ShowErrorDialog("Введите URL репозитория");
                return;
            }

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                await ShowErrorDialog("URL должен начинаться с http:// или https://");
                return;
            }

            try
            {
                await _dataService.AddRepositoryAsync(url);
                RepositoryUrlTextBox.Text = "";
                LoadRepositories();
            }
            catch (Exception ex)
            {
                await ShowErrorDialog($"Ошибка: {ex.Message}");
            }
        }        

        private void RepositoriesListBox_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Repository repo)
            {
                Frame.Navigate(typeof(RepoPropsPage), repo);
            }
        }

        private async Task ShowErrorDialog(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "Ошибка",
                Content = message,
                CloseButtonText = "ОК"
            };
            await dialog.ShowAsync();
        }
        //TODO: доделать переключение светлой и темной тем
        private void ApplyTheme(string themeTag)
        {
            ElementTheme theme;
            switch (themeTag)
            {
                case "Light":
                    theme = ElementTheme.Light;
                    break;
                case "Dark":
                    theme = ElementTheme.Dark;
                    break;
                case "Default":
                default:
                    theme = ElementTheme.Default;
                    break;
            }
            if (Window.Current.Content is FrameworkElement rootElement)
            {
                rootElement.RequestedTheme = theme;
            }
        }

        private void LoadAppTypeSettings()
        {
            // Устанавливаем флаг перед тем, как менять значения программно
            _isInitializing = true;

            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                SilverlightCheckBox.IsChecked = GetBoolSetting(localSettings, "ShowAppTypeSilverlight", true);
                WinRTCheckBox.IsChecked = GetBoolSetting(localSettings, "ShowAppTypeWinRT", true);
                UwpCheckBox.IsChecked = GetBoolSetting(localSettings, "ShowAppTypeUWP", true);
            }
            finally
            {
                // Снимаем флаг в блоке finally, чтобы он гарантированно отключился после загрузки
                _isInitializing = false;
            }
        }

        private void AppTypeFilter_Changed(object sender, RoutedEventArgs e)
        {
            // Если сейчас происходит загрузка настроек, игнорируем срабатывание события
            if (_isInitializing)
                return;

            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["ShowAppTypeSilverlight"] = SilverlightCheckBox.IsChecked == true;
            localSettings.Values["ShowAppTypeWinRT"] = WinRTCheckBox.IsChecked == true;
            localSettings.Values["ShowAppTypeUWP"] = UwpCheckBox.IsChecked == true;

            _dataService?.ClearCache();
        }

        private bool GetBoolSetting(ApplicationDataContainer localSettings, string key, bool defaultValue)
        {
            if (localSettings.Values.TryGetValue(key, out object value) && value is bool boolValue)
                return boolValue;
            return defaultValue;
        }

        private async void ClearCacheButton_Click(object sender, RoutedEventArgs e)
        {
            _dataService.ClearCache();
            var dialog = new ContentDialog
            {
                Title = "(o^▽^o)",
                Content = "Очистка кэша завершена",
                CloseButtonText = "ОК"
            };
            await dialog.ShowAsync();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}

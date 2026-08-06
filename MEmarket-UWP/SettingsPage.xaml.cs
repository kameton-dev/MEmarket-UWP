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
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Resources.Core;
using Windows.Globalization;

namespace MEmarket_UWP
{
    public sealed partial class SettingsPage : Page
    {
        private DataService _dataService;
        private bool _isInitializing;
        private readonly Windows.ApplicationModel.Resources.ResourceLoader loader = Windows.ApplicationModel.Resources.ResourceLoader.GetForCurrentView();


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
            LoadAppVersion();
            LoadLanguageSetting();

            var localSettings = ApplicationData.Current.LocalSettings;
            bool isEnabled = false;
            
            if (localSettings.Values.ContainsKey("BackgroundUpdateCheckEnabled"))
            {
                var savedValue = localSettings.Values["BackgroundUpdateCheckEnabled"];
                if (savedValue is bool)
                {
                    isEnabled = (bool)savedValue;
                }
            }
            
            BackgroundUpdateToggle.Toggled -= BackgroundUpdateToggle_Toggled;

            BackgroundUpdateToggle.IsOn = isEnabled;

            BackgroundUpdateToggle.Toggled += BackgroundUpdateToggle_Toggled;
        }

        private void LoadRepositories()
        {
            RepositoriesListBox.ItemsSource = _dataService.Repositories;
        }

        private void BackgroundUpdateToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            
            localSettings.Values["BackgroundUpdateCheckEnabled"] = BackgroundUpdateToggle.IsOn;
        }

        private async void AddRepositoryButton_Click(object sender, RoutedEventArgs e)
        {
            var url = RepositoryUrlTextBox.Text.Trim();
            
            if (string.IsNullOrEmpty(url))
            {
                await ShowErrorDialog(loader.GetString("RepoUrlInputMessage"));
                return;
            }

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                await ShowErrorDialog(loader.GetString("RepoUrlError"));
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
                await ShowErrorDialog(loader.GetString("ErrorText") + " " + ex.Message);
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
                Title = "(⇀‸↼‶)",
                Content = message,
                PrimaryButtonText = loader.GetString("OkButton"),
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
                _isInitializing = false;
            }
        }

        private void AppTypeFilter_Changed(object sender, RoutedEventArgs e)
        {
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
                Content = loader.GetString("ClearCacheDone"),
                PrimaryButtonText = loader.GetString("OkButton"),
            };
            await dialog.ShowAsync();
        }

        private void LoadLanguageSetting()
        {
            _isInitializing = true;
            try
            {
                var localSettings = ApplicationData.Current.LocalSettings;
                string currentLanguage = null;

                if (localSettings.Values.TryGetValue("AppLanguage", out object savedLanguage) && savedLanguage is string savedLanguageTag && !string.IsNullOrEmpty(savedLanguageTag))
                {
                    currentLanguage = savedLanguageTag;
                }
                else if (!string.IsNullOrEmpty(ApplicationLanguages.PrimaryLanguageOverride))
                {
                    currentLanguage = ApplicationLanguages.PrimaryLanguageOverride;
                }
                else
                {
                    currentLanguage = ApplicationLanguages.Languages.FirstOrDefault();
                }

                var selectedTag = currentLanguage != null && currentLanguage.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
                    ? "ru-RU"
                    : "en-US";

                LanguageComboBox.SelectedValue = selectedTag;
                if (LanguageComboBox.SelectedIndex < 0)
                {
                    LanguageComboBox.SelectedIndex = selectedTag == "ru-RU" ? 0 : 1;
                }
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private async void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing)
                return;

            if (LanguageComboBox.SelectedValue is string languageTag)
            {
                var currentLanguage = !string.IsNullOrEmpty(ApplicationLanguages.PrimaryLanguageOverride)
                    ? ApplicationLanguages.PrimaryLanguageOverride
                    : ApplicationLanguages.Languages.FirstOrDefault();

                if (!string.Equals(currentLanguage, languageTag, StringComparison.OrdinalIgnoreCase))
                {
                    ApplicationLanguages.PrimaryLanguageOverride = languageTag;
                    ApplicationData.Current.LocalSettings.Values["AppLanguage"] = languageTag;

                    var dialog = new ContentDialog
                    {
                        Title = "(ᵔ◡ᵔ)",
                        Content = loader.GetString("AppRestartMessage"),
                        PrimaryButtonText = loader.GetString("RestartButton"),
                        SecondaryButtonText = loader.GetString("LaterButton")
                    };

                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        await RestartAppAsync();
                    }
                }
            }
        }

        private async Task RestartAppAsync()
        {
            try
            {
                var result = await CoreApplication.RequestRestartAsync(string.Empty);
                if (result != (AppRestartFailureReason)0)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "(¬_¬”)",
                        Content = loader.GetString("AppRestartFailed"),
                        PrimaryButtonText = loader.GetString("OkButton"),
                    };
                    await errorDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "(¬_¬”)",
                    Content = loader.GetString("AppRestartError") + "/n" + ex.Message,
                    PrimaryButtonText = loader.GetString("OkButton"),
                };
                await errorDialog.ShowAsync();
            }
        }

        private void LoadAppVersion()
        {
            try
            {
                Package package = Package.Current;
                PackageId packageId = package.Id;
                PackageVersion version = packageId.Version;
                
                string formattedVersion = $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";

                // префикс беты
                AppVersionText.Text = formattedVersion + " (Public Beta)";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ыпыпыпып {ex.Message}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using MEmarket_UWP.Services;
using MEmarket_UWP.Models;

namespace MEmarket_UWP
{
    public sealed partial class AppPage : Page
    {
        private DataService _dataService;
        private AppItem _currentApp;
        private AppVersionInfo _selectedVersion;
        private DispatcherTimer _downloadAnimationTimer;
        private int _downloadDotCount;

        public AppPage()
        {
            this.InitializeComponent();

            _downloadAnimationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _downloadAnimationTimer.Tick += DownloadAnimationTimer_Tick;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _dataService = DataService.GetInstance();
            _currentApp = _dataService.CurrentApp;

            if (_currentApp != null)
            {
                UpdateAppInfo();
            }
        }

        private void UpdateAppInfo()
        {
            if (_currentApp == null)
                return;

            AppNameText.Text = _currentApp.Name;

            DescriptionText.Text = _currentApp.Description;

            if (!string.IsNullOrEmpty(_currentApp.Icon))
            {
                var iconValue = _currentApp.Icon.Trim();
                try
                {
                    var uriString = iconValue.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? iconValue
                        : $"ms-appx:///{iconValue}";
                    AppIcon.Source = new BitmapImage(new Uri(uriString, UriKind.Absolute));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading icon: {ex.Message}");
                }
            }

            PublisherText.Text = $"{_currentApp.Publisher}";

            AppSizeText.Text = $"Размер: {(_currentApp.Size ?? "Неизвестно")}";

            UpdateVersionDisplay();
            UpdateCertificateAndMinVersionDisplay();
            UpdateTargetOsDisplay();

            if (_currentApp.Screenshots != null && _currentApp.Screenshots.Count > 0)
            {
                ScreenshotsScrollViewer.Visibility = Visibility.Visible;
                ScreenshotsListView.ItemsSource = _currentApp.Screenshots;
            }
            else
            {
                ScreenshotsScrollViewer.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateCertificateAndMinVersionDisplay()
        {
            /* if (!string.IsNullOrEmpty(_currentApp.CertificateUrl))
            {
                DownloadCerButton.Visibility = Visibility.Visible;
            }
            else
            {
                DownloadCerButton.Visibility = Visibility.Collapsed;
            } */

            if (!string.IsNullOrEmpty(_currentApp.MinVersion))
            {
                MinVersionText.Visibility = Visibility.Visible;
                MinVersionText.Text = $"Мин. версия: {_currentApp.MinVersion}";
            }
            else
            {
                MinVersionText.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateTargetOsDisplay()
        {
            TargetOsPanel.Children.Clear();

            if (_currentApp == null || string.IsNullOrEmpty(_currentApp.OS))
            {
                TargetOsTextBlock.Visibility = Visibility.Collapsed;
                TargetOsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            TargetOsTextBlock.Visibility = Visibility.Visible;
            TargetOsPanel.Visibility = Visibility.Visible;

            var osValue = _currentApp.OS.Trim().ToLowerInvariant();

            if (osValue.Contains("wp8.1"))
            {
                AddOsImage("Assets/images/wp8.1.png");
            }

            if (osValue.Contains("w10m"))
            {
                AddOsImage("Assets/images/w10m.png");
            }
        }

        private void AddOsImage(string imagePath)
        {
            TargetOsPanel.Children.Add(new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///" + imagePath)),
                Height = 50,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 8, 0)
            });
        }

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            if (_currentApp == null)
            {
                await ShowDialogAsync("(｡╯︵╰｡) ", "Ссылка на скачивание недоступна");
                return;
            }

            var downloadUrl = _selectedVersion?.DownloadUrl ?? _currentApp.DownloadUrl;
            if (string.IsNullOrEmpty(downloadUrl))
            {
                await ShowDialogAsync("(｡╯︵╰｡) ", "Ссылка на скачивание недоступна");
                return;
            }

            await DownloadAndInstallAsync(downloadUrl);
        }

        /*
        private async void DownloadCerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentApp == null || string.IsNullOrEmpty(_currentApp.CertificateUrl))
            {
                await ShowDialogAsync("(｡╯︵╰｡) ", "Ссылка на сертификат недоступна");
                return;
            }

            await DownloadAndOpenCertificateAsync(_currentApp.CertificateUrl);
        }
        */

        private async Task DownloadAndOpenCertificateAsync(string certificateUrl)
        {
            await UpdateSystemStatusAsync("Загрузка сертификата");
            _downloadDotCount = 0;
            _downloadAnimationTimer.Start();

            try
            {
                var uri = new Uri(certificateUrl);
                var fileName = Path.GetFileName(uri.LocalPath);
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = "downloaded_certificate.cer";
                }

                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(uri);
                    response.EnsureSuccessStatusCode();

                    using (var httpResponse = response)
                    {
                        await SaveHttpContentToFileAsync(httpResponse, file);
                    }
                }

                await UpdateSystemStatusAsync("Сертификат загружен. Открытие...");

                var launchOptions = new LauncherOptions
                {
                    DisplayApplicationPicker = false
                };

                var launched = await Launcher.LaunchFileAsync(file, launchOptions);
                if (!launched)
                {
                    await ShowDialogAsync("(｡╯︵╰｡) ", "Не удалось открыть сертификат.");
                }
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("(｡╯︵╰｡) ", $"Не удалось скачать сертификат: {ex.Message}");
            }
            finally
            {
                _downloadAnimationTimer.Stop();
                await HideSystemStatusAsync();
            }
        }

        private async Task DownloadAndInstallAsync(string downloadUrl)
        {
            await UpdateSystemStatusAsync("Загрузка");
            _downloadDotCount = 0;
            _downloadAnimationTimer.Start();

            try
            {
                var uri = new Uri(downloadUrl);
                var fileName = Path.GetFileName(uri.LocalPath);
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = "downloaded_app.appx";
                }

                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                if (extension == ".xap")
                {
                    await ShowDialogAsync("Не так быстро ;)", "Файл XAP не поддерживает развертывание на W10M.");
                    await HideSystemStatusAsync();
                    return;
                }
                else if (extension != ".appx" && extension != ".appxbundle" && extension != ".msix" && extension != ".msixbundle")
                {
                    var allow = await ConfirmAsync("Формат файла не типичен для установки приложения. Продолжить скачивание и попытку установки?");
                    if (!allow)
                    {
                        await HideSystemStatusAsync();
                        return;
                    }
                }

                var folder = ApplicationData.Current.LocalFolder;
                var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(uri);
                    response.EnsureSuccessStatusCode();

                    using (var httpResponse = response)
                    {
                        await SaveHttpContentToFileAsync(httpResponse, file);
                    }
                }

                await UpdateSystemStatusAsync("Скачивание завершено");
                _downloadAnimationTimer.Stop();

                var launchOptions = new LauncherOptions
                {
                    DisplayApplicationPicker = false
                };

                var launched = await Launcher.LaunchFileAsync(file, launchOptions);
                if (!launched)
                {
                    await ShowDialogAsync("Ошибка", "Не удалось открыть файл установки.");
                }
            }
            catch (Exception ex)
            {
                _downloadAnimationTimer.Stop();
                await HideSystemStatusAsync();
                await ShowDialogAsync("Ошибка", $"Не удалось скачать приложение: {ex.Message}");
                return;
            }
            finally
            {
                _downloadAnimationTimer.Stop();
                await HideSystemStatusAsync();
            }
        }

        private async Task<bool> ConfirmAsync(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "(＾＾＃) ",
                Content = message,
                PrimaryButtonText = "Продолжить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        private void DownloadAnimationTimer_Tick(object sender, object e)
        {
        }

        private async Task SaveHttpContentToFileAsync(HttpResponseMessage response, StorageFile file)
        {
            var contentLength = response.Content.Headers.ContentLength;
            DownloadProgressBar.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = 0;
            DownloadProgressBar.IsIndeterminate = !contentLength.HasValue;

            using (var inputStream = (await response.Content.ReadAsInputStreamAsync()).AsStreamForRead())
            using (var outputStream = await file.OpenAsync(FileAccessMode.ReadWrite))
            using (var destinationStream = outputStream.GetOutputStreamAt(0).AsStreamForWrite())
            {
                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;
                while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await destinationStream.WriteAsync(buffer, 0, bytesRead);
                    totalRead += bytesRead;
                    if (contentLength.HasValue)
                    {
                        DownloadProgressBar.Value = (double)totalRead / contentLength.Value * 100d;
                    }
                }

                await destinationStream.FlushAsync();
            }
        }

        private Task UpdateSystemStatusAsync(string text)
        {
            DownloadProgressBar.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = 0;
            DownloadProgressBar.IsIndeterminate = true;
            return Task.CompletedTask;
        }

        private Task HideSystemStatusAsync()
        {
            DownloadProgressBar.Visibility = Visibility.Collapsed;
            DownloadProgressBar.IsIndeterminate = false;
            DownloadProgressBar.Value = 0;
            return Task.CompletedTask;
        }

        private void UpdateVersionDisplay()
        {
            if (_currentApp.Versions != null && _currentApp.Versions.Count > 1)
            {
                AppVersionText.Visibility = Visibility.Collapsed;
                VersionComboBox.Visibility = Visibility.Visible;

                var sortedVersions = _currentApp.Versions
                    .OrderByDescending(v => v.Version, Comparer<string>.Create(CompareVersionStrings))
                    .ToList();

                VersionComboBox.ItemsSource = sortedVersions;
                VersionComboBox.SelectedIndex = 0;
                _selectedVersion = VersionComboBox.SelectedItem as AppVersionInfo;
            }
            else
            {
                AppVersionText.Visibility = Visibility.Visible;
                VersionComboBox.Visibility = Visibility.Collapsed;
                AppVersionText.Text = $"Версия: {(_currentApp.Version ?? "Неизвестно")}";
                _selectedVersion = null;
            }
        }

        private int CompareVersionStrings(string x, string y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            if (Version.TryParse(x, out var vx) && Version.TryParse(y, out var vy))
            {
                return vx.CompareTo(vy);
            }

            var partsX = x.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var partsY = y.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            var length = Math.Min(partsX.Length, partsY.Length);

            for (int i = 0; i < length; i++)
            {
                if (Version.TryParse(partsX[i], out var px) && Version.TryParse(partsY[i], out var py))
                {
                    var cmp = px.CompareTo(py);
                    if (cmp != 0) return cmp;
                }
                else
                {
                    var cmp = string.Compare(partsX[i], partsY[i], StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0) return cmp;
                }
            }

            return partsX.Length.CompareTo(partsY.Length);
        }

        private async Task ShowDialogAsync(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "ОК"
            };

            await dialog.ShowAsync();
        }

        private void VersionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = VersionComboBox.SelectedItem as AppVersionInfo;
            if (selected != null)
            {
                _selectedVersion = selected;
            }
        }

        private void IconImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("эъъээъэъъ");
        }

        private async void Screenshot_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var image = sender as Image;
            if (image?.Source is BitmapImage bitmapImage && bitmapImage.UriSource != null)
            {
                var dialog = new ContentDialog
                {
                    Title = "",
                    Content = new Image { Source = bitmapImage, Width = 300, Height = 400, Stretch = Stretch.Uniform },
                    CloseButtonText = "Закрыть"
                };
                await dialog.ShowAsync();
            }
        }
    }
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
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
using Windows.Data.Json;
using Windows.UI.Xaml.Documents;

namespace MEmarket_UWP
{
    public sealed partial class AppPage : Page
    {
        private DataService _dataService;
        private AppItem _currentApp;
        private AppVersionInfo _selectedVersion;
        private DispatcherTimer _downloadAnimationTimer;
        private int _downloadDotCount;
        private bool _isDescriptionExpanded;

        public AppPage()
        {
            this.InitializeComponent();

            _downloadAnimationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _downloadAnimationTimer.Tick += DownloadAnimationTimer_Tick;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _dataService = DataService.GetInstance();
            _currentApp = _dataService.CurrentApp;

            if (_currentApp != null)
            {
                await LoadEntryJsonAsync(_currentApp);
                UpdateAppInfo();
            }
        }

        private void UpdateAppInfo()
        {
            if (_currentApp == null)
                return;

            _isDescriptionExpanded = false;

            AppNameText.Text = _currentApp.Name;

            SummaryText.Text = string.IsNullOrEmpty(_currentApp.Summary) ? _currentApp.Description : _currentApp.Summary;

            if (!string.IsNullOrEmpty(_currentApp.Description))
            {
                UpdateDescriptionText(_currentApp.Description);
            }

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

            /*AppSizeText.Text = $"Размер: {(_currentApp.Size ?? "Неизвестно")}";*/

            UpdateVersionDisplay();
            UpdateCertificateAndMinVersionDisplay();
            UpdateAppTypeDisplay();

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

        private async Task LoadEntryJsonAsync(AppItem app)
        {
            if (app == null || string.IsNullOrEmpty(app.EntryJsonUrl))
            {
                System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: app={app}, EntryJsonUrl={app?.EntryJsonUrl}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: Loading from {app.EntryJsonUrl}");

            try
            {
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(new Uri(app.EntryJsonUrl));
                    if (!response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: HTTP Error {response.StatusCode}");
                        return;
                    }

                    var jsonText = await response.Content.ReadAsStringAsync();
                    var root = JsonObject.Parse(jsonText);

                    var title = GetJsonString(root, "title");
                    if (!string.IsNullOrEmpty(title))
                        app.Name = title;

                    var description = GetJsonString(root, "description");
                    if (!string.IsNullOrEmpty(description))
                        app.Description = description;

                    var summary = GetJsonString(root, "summary");
                    if (!string.IsNullOrEmpty(summary))
                        app.Summary = summary;

                    var creator = GetJsonString(root, "creator");
                    if (!string.IsNullOrEmpty(creator))
                        app.Publisher = creator;

                    var appUrlValue = GetJsonString(root, "app_url");
                    if (!string.IsNullOrEmpty(appUrlValue))
                    {
                        app.AppUrl = appUrlValue;
                        System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: AppUrl set to {app.AppUrl}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: app_url not found in entry.json");
                    }

                    var appTypeValue = GetJsonString(root, "app_type");
                    if (!string.IsNullOrEmpty(appTypeValue))
                    {
                        app.AppType = appTypeValue;
                    }

                    var iconValue = GetJsonString(root, "icon");
                    if (!string.IsNullOrEmpty(iconValue))
                    {
                        app.Icon = CombineUrls(app.AppUrl, iconValue);
                        System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: Icon set to {app.Icon}");
                    }

                    var downloadUrl = GetJsonString(root, "download_url");
                    if (!string.IsNullOrEmpty(downloadUrl))
                        app.DownloadUrl = CombineUrls(app.AppUrl ?? app.BaseUrl, downloadUrl);

                    if (root.ContainsKey("capabilities") && root["capabilities"].ValueType == JsonValueType.Array)
                    {
                        app.Capabilities.Clear();
                        var capsArray = root.GetNamedArray("capabilities");
                        for (int i = 0; i < (int)capsArray.Count; i++)
                        {
                            if (capsArray[i].ValueType == JsonValueType.String)
                            {
                                app.Capabilities.Add(capsArray[i].GetString());
                            }
                        }
                    }

                    var certificateUrl = GetJsonString(root, "cer_url");
                    if (!string.IsNullOrEmpty(certificateUrl))
                        app.CertificateUrl = CombineUrls(app.BaseUrl, certificateUrl);

                    if (root.ContainsKey("screenshots") && root["screenshots"].ValueType == JsonValueType.Array)
                    {
                        app.Screenshots.Clear();
                        var screenshotsArray = root.GetNamedArray("screenshots");
                        for (int i = 0; i < (int)screenshotsArray.Count; i++)
                        {
                            try
                            {
                                if (screenshotsArray[i].ValueType == JsonValueType.String)
                                {
                                    var screenshotPath = screenshotsArray[i].GetString();
                                    var fullUrl = CombineUrls(app.AppUrl ?? app.BaseUrl, screenshotPath);
                                    app.Screenshots.Add(fullUrl);
                                }
                            }
                            catch { }
                        }
                    }

                    if (root.ContainsKey("versions") && root["versions"].ValueType == JsonValueType.Array)
                    {
                        System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: Found versions array");
                        app.Versions.Clear();
                        var versionsArray = root.GetNamedArray("versions");
                        System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: Version count = {versionsArray.Count}");
                        for (int i = 0; i < (int)versionsArray.Count; i++)
                        {
                            try
                            {
                                var versionObj = versionsArray[i].GetObject();
                                var versionText = GetJsonString(versionObj, "version");
                                var versionDownloadUrl = GetJsonString(versionObj, "download_url");
                                var appFile = GetJsonString(versionObj, "app_file");
                                
                                System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: Version={versionText}, appFile={appFile}, downloadUrl={versionDownloadUrl}");
                                
                                if (string.IsNullOrEmpty(versionDownloadUrl) && !string.IsNullOrEmpty(appFile))
                                {
                                    versionDownloadUrl = CombineUrls(app.AppUrl ?? app.BaseUrl, appFile);
                                    System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: Generated downloadUrl={versionDownloadUrl}");
                                }
                                if (!string.IsNullOrEmpty(versionText) && !string.IsNullOrEmpty(versionDownloadUrl))
                                {
                                    app.Versions.Add(new AppVersionInfo
                                    {
                                        Version = versionText,
                                        DownloadUrl = versionDownloadUrl
                                    });
                                    System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: Added version {versionText}");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: Error parsing version: {ex.Message}");
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: Total versions loaded = {app.Versions.Count}");
                        if (app.Versions.Count > 0)
                        {
                            app.Versions = app.Versions
                                .OrderByDescending(v => v.Version, Comparer<string>.Create(CompareVersionStrings))
                                .ToList();

                            var firstVersion = app.Versions[0];
                            app.Version = firstVersion.Version;
                            app.DownloadUrl = firstVersion.DownloadUrl;
                            System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: Set app.DownloadUrl = {app.DownloadUrl}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"LoadEntryJsonAsync: No versions array found in entry.json");
                    }
                }
                
                UpdateVersionDisplay();
                UpdateCertificateAndMinVersionDisplay();
                UpdateAppTypeDisplay();
                if (!string.IsNullOrEmpty(app.Description))
                {
                    UpdateDescriptionText(app.Description);
                }
                
                if (app.Screenshots != null && app.Screenshots.Count > 0)
                {
                    ScreenshotsScrollViewer.Visibility = Visibility.Visible;
                    ScreenshotsListView.ItemsSource = app.Screenshots;
                }
            }
            catch { }
        }

        private string GetJsonString(JsonObject obj, string key)
        {
            if (obj.ContainsKey(key))
            {
                var value = obj.GetNamedValue(key);
                if (value.ValueType == JsonValueType.String)
                {
                    return value.GetString();
                }
            }
            return string.Empty;
        }

        private string CombineUrls(string baseUrl, string relativePath)
        {
            if (!string.IsNullOrEmpty(relativePath) &&
                (relativePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 relativePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                 relativePath.StartsWith("//")))
            {
                return relativePath;
            }

            if (string.IsNullOrEmpty(relativePath))
                return baseUrl;

            if (string.IsNullOrEmpty(baseUrl))
                return relativePath;

            if (!baseUrl.EndsWith("/"))
                baseUrl += "/";

            if (relativePath.StartsWith("/"))
                relativePath = relativePath.Substring(1);

            return baseUrl + relativePath;
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
            } 

            if (!string.IsNullOrEmpty(_currentApp.MinVersion))
            {
                MinVersionText.Visibility = Visibility.Visible;
                MinVersionText.Text = $"Мин. версия: {_currentApp.MinVersion}";
            }
            else
            {
                MinVersionText.Visibility = Visibility.Collapsed;
            } */

            if (_currentApp.Capabilities != null && _currentApp.Capabilities.Count > 0)
            {
                CapabilitiesText.Visibility = Visibility.Visible;
                CapabilitiesList.Visibility = Visibility.Visible;
                CapabilitiesList.ItemsSource = _currentApp.Capabilities;
            }
            else
            {
                CapabilitiesText.Visibility = Visibility.Collapsed;
                CapabilitiesList.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateAppTypeDisplay()
        {
            if (_currentApp == null || string.IsNullOrEmpty(_currentApp.AppType))
            {
                AppTypeText.Visibility = Visibility.Collapsed;
                return;
            }

            AppTypeText.Visibility = Visibility.Visible;
            AppTypeText.Text = $"Тип приложения: {_currentApp.AppType}";
        }

        private void ToggleDescriptionButton_Click(object sender, RoutedEventArgs e)
        {
            _isDescriptionExpanded = !_isDescriptionExpanded;
            ToggleDescriptionButton.Content = _isDescriptionExpanded ? "Свернуть" : "Развернуть";
            DescriptionText.MaxLines = _isDescriptionExpanded ? int.MaxValue : 5;
        }

        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Download_Click: _currentApp={_currentApp}, _selectedVersion={_selectedVersion}");
            
            if (_currentApp == null)
            {
                await ShowDialogAsync("(｡╯︵╰｡) ", "Ссылка на скачивание недоступна");
                return;
            }

            var downloadUrl = _selectedVersion?.DownloadUrl ?? _currentApp.DownloadUrl;
            System.Diagnostics.Debug.WriteLine($"Download_Click: downloadUrl={downloadUrl}");
            
            if (string.IsNullOrEmpty(downloadUrl))
            {
                System.Diagnostics.Debug.WriteLine($"Download_Click: DownloadUrl is empty. _selectedVersion?.DownloadUrl={_selectedVersion?.DownloadUrl}, _currentApp.DownloadUrl={_currentApp.DownloadUrl}");
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
                    var dialog = new ContentDialog
                    {
                        Title = "Не так быстро ;)",
                        Content = "Файл XAP не поддерживает развертывание на W10M.",
                        PrimaryButtonText = "Сохранить файл",
                        CloseButtonText = "ОК",
                        DefaultButton = ContentDialogButton.Close
                    };

                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        await SaveXapFileAsync(uri, fileName);
                    }

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

        private async Task SaveXapFileAsync(Uri uri, string fileName)
        {
            try
            {
                var folderPicker = new FolderPicker
                {
                    SuggestedStartLocation = PickerLocationId.Downloads
                };
                folderPicker.FileTypeFilter.Add("*");

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder == null)
                {
                    return;
                }

                await UpdateSystemStatusAsync("Сохранение файла");
                _downloadAnimationTimer.Start();

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

                await ShowDialogAsync("Готово", $"Файл сохранен в папку: {folder.Path}");
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Ошибка", $"Не удалось сохранить файл: {ex.Message}");
            }
            finally
            {
                _downloadAnimationTimer.Stop();
                await HideSystemStatusAsync();
            }
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

        private void UpdateDescriptionText(string description)
        {
            if (string.IsNullOrEmpty(description))
                return;

            DescriptionText.MaxLines = _isDescriptionExpanded ? int.MaxValue : 5;
            
            MarkdownRenderer.Render(DescriptionText, description);

            if (HasLongDescription(description))
            {
                ToggleDescriptionButton.Visibility = Visibility.Visible;
                ToggleDescriptionButton.Content = _isDescriptionExpanded ? "Свернуть" : "Развернуть";
            }
            else
            {
                ToggleDescriptionButton.Visibility = Visibility.Collapsed;
            }
        }

        private bool HasLongDescription(string description)
        {
            if (string.IsNullOrEmpty(description))
                return false;

            var lines = description.Split(new[] { '\n' }, StringSplitOptions.None);
            if (lines.Length > 5)
                return true;

            var estimatedLines = 0;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                {
                    estimatedLines++;
                    continue;
                }

                estimatedLines += Math.Max(1, (trimmed.Length + 79) / 80);
                if (estimatedLines > 5)
                    return true;
            }

            return false;
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


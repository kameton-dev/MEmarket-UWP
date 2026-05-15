using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.Web.Http;
using MEmarket_UWP.Models;
using MEmarket_UWP.Services;

namespace MEmarket_UWP
{
    public sealed partial class RepoPropsPage : Page
    {
        private Repository _currentRepository;
        private readonly DataService _dataService;

        public RepoPropsPage()
        {
            this.InitializeComponent();
            _dataService = DataService.GetInstance();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Repository repo)
            {
                _currentRepository = repo;
                RepositoryTitleTextBlock.Text = string.IsNullOrEmpty(repo.Name) ? "НАЗВАНИЕ РЕПОЗИТОРИЯ" : repo.Name;
                RepositoryUrlTextBlock.Text = repo.Url;
                CreatorTextBlock.Text = string.IsNullOrEmpty(repo.Creator) ? "Не указано" : repo.Creator;
                LastUpdatedTextBlock.Text = string.IsNullOrEmpty(repo.LastUpdated) ? "Не указано" : repo.LastUpdated;
                await LoadRepositoryPropertiesAsync(repo.Url);
            }
        }

        private async Task LoadRepositoryPropertiesAsync(string url)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var cacheBuster = DateTime.Now.Ticks.ToString();
                    var response = await httpClient.GetStringAsync(new Uri(url + "?v=" + cacheBuster));
                    var root = JsonObject.Parse(response);

                    if (root.ContainsKey("repo_name") && root["repo_name"].ValueType == JsonValueType.String)
                    {
                        var repoName = root["repo_name"].GetString();
                        if (!string.IsNullOrEmpty(repoName))
                        {
                            RepositoryTitleTextBlock.Text = repoName;
                        }
                    }

                    if (root.ContainsKey("creator") && root["creator"].ValueType == JsonValueType.String)
                    {
                        CreatorTextBlock.Text = root["creator"].GetString();
                    }

                    if (root.ContainsKey("last_updated") && root["last_updated"].ValueType == JsonValueType.String)
                    {
                        LastUpdatedTextBlock.Text = root["last_updated"].GetString();
                    }

                    if (root.ContainsKey("supported_os") && root["supported_os"].ValueType == JsonValueType.Array)
                    {
                        var supportedOsArray = root["supported_os"].GetArray();
                        var supportedOsList = new List<string>();
                        foreach (var item in supportedOsArray)
                        {
                            if (item.ValueType == JsonValueType.String)
                            {
                                supportedOsList.Add(item.GetString());
                            }
                        }
                        UpdateSupportedOsUI(supportedOsList);
                    }
                    else
                    {
                        UpdateSupportedOsUI(null);
                    }
                }
            }
            catch (Exception)
            {
                CreatorTextBlock.Text = "Не удалось загрузить информацию";
                LastUpdatedTextBlock.Text = "Не удалось загрузить информацию";
                UpdateSupportedOsUI(null);
            }
        }

        private void UpdateSupportedOsUI(IReadOnlyList<string> supportedOs)
        {
            SupportedOsPanel.Children.Clear();

            if (supportedOs == null || supportedOs.Count == 0)
            {
                SupportedOsPanel.Children.Add(CreateNoInfoTextBlock());
                return;
            }

            foreach (var os in supportedOs)
            {
                if (string.Equals(os, "WP8.1", StringComparison.OrdinalIgnoreCase))
                {
                    AddSupportedOsImage("Assets/images/wp8.1.png");
                }
                else if (string.Equals(os, "W10M", StringComparison.OrdinalIgnoreCase) || string.Equals(os, "Windows 10 Mobile", StringComparison.OrdinalIgnoreCase))
                {
                    AddSupportedOsImage("Assets/images/w10m.png");
                }
            }

            if (SupportedOsPanel.Children.Count == 0)
            {
                SupportedOsPanel.Children.Add(CreateNoInfoTextBlock());
            }
        }

        private TextBlock CreateNoInfoTextBlock()
        {
            return new TextBlock
            {
                Text = "Не указано",
                Style = (Style)Resources["BodyTextBlockStyle"],
                TextWrapping = TextWrapping.Wrap
            };
        }

        private void AddSupportedOsImage(string imagePath)
        {
            SupportedOsPanel.Children.Add(new Image
            {
                Source = new BitmapImage(new Uri("ms-appx:///" + imagePath)),
                Height = 50,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 0, 8)
            });
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private async void DeleteRepoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRepository == null)
                return;

            if (_currentRepository.Url == "http://millenniummarket.ru/properties.json")
            {
                var dialog = new ContentDialog
                {
                    Title = "(￣▽￣)",
                    Content = "Этот репозиторий является системным и не может быть удалён",
                    CloseButtonText = "ОК"
                };
                await dialog.ShowAsync();
                return;
            }

            try
            {
                await _dataService.RemoveRepositoryAsync(_currentRepository.Url);
                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "╮(￣ω￣;)╭ ",
                    Content = $"Не удалось удалить репозиторий: {ex.Message}",
                    CloseButtonText = "ОК"
                };
                await dialog.ShowAsync();
            }
        }
    }
}

using System;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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
                    var response = await httpClient.GetStringAsync(new Uri(GetIndexUrl(url) + "?v=" + cacheBuster));
                    var root = JsonObject.Parse(response);


                    if (root.ContainsKey("creator") && root["creator"].ValueType == JsonValueType.String)
                    {
                        CreatorTextBlock.Text = root["creator"].GetString();
                    }

                    if (root.ContainsKey("last_updated") && root["last_updated"].ValueType == JsonValueType.String)
                    {
                        LastUpdatedTextBlock.Text = root["last_updated"].GetString();
                    }

                }
            }
            catch (Exception)
            {
                CreatorTextBlock.Text = "Не удалось загрузить информацию";
                LastUpdatedTextBlock.Text = "Не удалось загрузить информацию";
            }
        }


        private string GetIndexUrl(string repoRootUrl)
        {
            if (string.IsNullOrEmpty(repoRootUrl))
                return string.Empty;

            if (repoRootUrl.EndsWith("/"))
                return repoRootUrl + "index.json";

            return repoRootUrl + "/index.json";
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

            if (_currentRepository.Url == "http://millenniummarket.ru" || _currentRepository.Url == "http://millenniummarket.ru/properties.json")
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

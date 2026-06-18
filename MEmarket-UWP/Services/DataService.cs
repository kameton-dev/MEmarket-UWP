using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;
using Windows.Web.Http;
using MEmarket_UWP.DataModel;
using MEmarket_UWP.Models;

namespace MEmarket_UWP.Services
{
    public class DataService
    {
        private static DataService _instance;
        private static readonly object _lock = new object();

        private const string DEFAULT_REPO_URL = "http://millenniummarket.ru";
        private const string INDEX_FILE_NAME = "index.json";

        private static readonly Dictionary<string, string> _categoryOrder = new Dictionary<string, string>
        {
            {"games", "Игры"},
            {"store", "Др. магазины приложений"},
            {"entertaintment", "Развлечения"},
            {"music+video", "Музыка + Видео"},
            {"tools", "Инструменты"},
            {"livestyle", "Лайвстайл"},
            {"news+weather", "Новости + погода"},
            {"health+fitness", "Здоровье + фитнес"},
            {"photo", "Фото"},
            {"social", "Социальные"},
            {"sports", "Спорт"},
            {"business", "Бизнес"},
            {"education", "Обучение"}
        };

        private static readonly Dictionary<string, string> _categoryGlyphs = new Dictionary<string, string>
        {
            {"games", "\uE7FC"},
            {"store", "\uE719"},
            {"entertaintment", "\uE768"},
            {"music+video", "\uE189"},
            {"tools", "\uE15E"},
            {"livestyle", "\uE125"},
            {"news+weather", "\uE8B5"},
            {"health+fitness", "\uE71A"},
            {"photo", "\uE722"},
            {"social", "\uE13D"},
            {"sports", "\uE128"},
            {"business", "\uE821"},
            {"education", "\uE7BE"},
            {OTHER_CATEGORY_KEY, "\uEA86"}
        };

        private const string OTHER_CATEGORY = "Прочее";
        private const string OTHER_CATEGORY_KEY = "other";

        private ObservableCollection<Repository> _repositories;
        private Dictionary<string, List<AppItem>> _appsCache;
        private Dictionary<string, List<CategoryData>> _categoriesCache;
        private AppItem _currentApp;

        public ObservableCollection<Repository> Repositories => _repositories;
        public AppItem CurrentApp => _currentApp;

        public void SetCurrentApp(AppItem app)
        {
            _currentApp = app;
        }

        public static DataService GetInstance()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new DataService();
                    }
                }
            }
            return _instance;
        }

        private DataService()
        {
            _repositories = new ObservableCollection<Repository>();
            _appsCache = new Dictionary<string, List<AppItem>>();
            _categoriesCache = new Dictionary<string, List<CategoryData>>();
        }

        private bool ShouldShowApp(AppItem app)
        {
            if (app == null)
                return false;

            return ShouldShowAppType(app.AppType);
        }

        private bool ShouldShowAppType(string appType)
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            var showSilverlight = GetBoolSetting(localSettings, "ShowAppTypeSilverlight", true);
            var showWinRT = GetBoolSetting(localSettings, "ShowAppTypeWinRT", true);
            var showUwp = GetBoolSetting(localSettings, "ShowAppTypeUWP", true);

            if (string.IsNullOrWhiteSpace(appType))
            {
                return showSilverlight && showWinRT && showUwp;
            }

            var normalized = appType.Trim().ToLowerInvariant();
            if (normalized.Contains("silverlight"))
                return showSilverlight;
            if (normalized.Contains("winrt"))
                return showWinRT;
            if (normalized.Contains("uwp"))
                return showUwp;

            return showSilverlight && showWinRT && showUwp;
        }

        private bool GetBoolSetting(ApplicationDataContainer localSettings, string key, bool defaultValue)
        {
            if (localSettings.Values.TryGetValue(key, out object value) && value is bool boolValue)
            {
                return boolValue;
            }
            return defaultValue;
        }

        /// <summary>
        /// TODO: переделать это говно
        /// </summary>
        public async Task InitializeAsync()
        {
            await LoadRepositoriesAsync();
            
            if (_repositories.Count == 0)
            {
                _repositories.Add(new Repository
                {
                    Url = DEFAULT_REPO_URL,
                    Name = "MEmarket Basic",
                    Creator = "kameton.dev",
                    LastUpdated = DateTime.Now.ToString("yyyy-MM-dd")
                });
                await SaveRepositoriesAsync();
            }
        }

        /// <summary>
        /// Очистка кэша
        /// </summary>
        public void ClearCache()
        {
            _appsCache.Clear();
            _categoriesCache.Clear();
        }

        /// <summary>
        /// Добавление нового репозитория
        /// </summary>
        public async Task AddRepositoryAsync(string url)
        {
            var normalizedUrl = NormalizeRepositoryRootUrl(url);

            if (_repositories.Any(r => r.Url == normalizedUrl))
                throw new Exception("Репозиторий уже добавлен");

            var indexUrl = GetIndexUrl(normalizedUrl);

            using (var httpClient = new HttpClient())
            {
                try
                {
                    var response = await httpClient.GetAsync(new Uri(indexUrl));
                    response.EnsureSuccessStatusCode();
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var jsonObject = JsonObject.Parse(jsonString);

                    if (!jsonObject.ContainsKey("repo_name") || !jsonObject.ContainsKey("creator") || !jsonObject.ContainsKey("last_updated"))
                    {
                        throw new Exception("JSON не содержит обязательных полей: repo_name, creator, last_updated");
                    }

                    var repoName = jsonObject["repo_name"].GetString();
                    var creator = jsonObject["creator"].GetString();
                    var lastUpdated = jsonObject["last_updated"].GetString();

                    if (string.IsNullOrWhiteSpace(repoName) || string.IsNullOrWhiteSpace(creator) || string.IsNullOrWhiteSpace(lastUpdated))
                    {
                        throw new Exception("Обязательные поля не могут быть пустыми");
                    }

                    var repo = new Repository
                    {
                        Url = normalizedUrl,
                        Name = repoName,
                        Creator = creator,
                        LastUpdated = lastUpdated
                    };

                    _repositories.Add(repo);

                    if (_appsCache.ContainsKey(normalizedUrl))
                        _appsCache.Remove(normalizedUrl);
                    if (_categoriesCache.ContainsKey(normalizedUrl))
                        _categoriesCache.Remove(normalizedUrl);

                    await SaveRepositoriesAsync();
                }
                catch (Exception ex)
                {
                    throw new Exception($"Ошибка при добавлении репозитория: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Удаление репо
        /// </summary>
        public async Task RemoveRepositoryAsync(string url)
        {
            var repo = _repositories.FirstOrDefault(r => r.Url == url);
            if (repo != null)
            {
                _repositories.Remove(repo);

                if (_appsCache.ContainsKey(url))
                    _appsCache.Remove(url);
                if (_categoriesCache.ContainsKey(url))
                    _categoriesCache.Remove(url);

                await SaveRepositoriesAsync();
            }
        }

        /// <summary>
        /// Получение категорий с репо
        /// </summary>
        public async Task<List<CategoryData>> GetCategoriesAsync()
        {
            var allCategories = new Dictionary<string, CategoryData>();
            var uniqueId = 0;

            foreach (var repo in _repositories)
            {
                var categories = await GetCategoriesFromRepositoryAsync(repo.Url);
                foreach (var cat in categories)
                {
                    var categoryKey = cat.Name.ToLower();
                    if (!_categoryOrder.ContainsKey(categoryKey))
                    {
                        categoryKey = OTHER_CATEGORY_KEY;
                    }
                    if (!allCategories.ContainsKey(categoryKey))
                    {
                        var displayName = _categoryOrder.ContainsKey(categoryKey) 
                            ? _categoryOrder[categoryKey] 
                            : OTHER_CATEGORY;

                        allCategories[categoryKey] = new CategoryData
                        {
                            Id = uniqueId.ToString(),
                            Key = categoryKey,
                            Name = displayName,
                            IconGlyph = GetCategoryGlyph(categoryKey)
                        };
                        uniqueId++;
                    }
                }
            }

            var sortedCategories = allCategories.Values
                .OrderBy(c => 
                {
                    if (c.Name == OTHER_CATEGORY)
                        return int.MaxValue;
                    
                    var key = _categoryOrder.FirstOrDefault(x => x.Value == c.Name).Key;
                    return Array.IndexOf(_categoryOrder.Keys.ToArray(), key);
                })
                .ToList();

            return sortedCategories;
        }

        /// <summary>
        /// Мдл2 иконки для категорий
        /// </summary>
        private string GetCategoryGlyph(string categoryKey)
        {
            if (string.IsNullOrEmpty(categoryKey))
                categoryKey = OTHER_CATEGORY_KEY;

            if (_categoryGlyphs.TryGetValue(categoryKey, out var glyph))
                return glyph;

            return _categoryGlyphs[OTHER_CATEGORY_KEY];
        }

        /// <summary>
        /// Ээээ  дцлуцщлащу мне лень писать 
        /// </summary>
        private async Task<List<CategoryData>> GetCategoriesFromRepositoryAsync(string repoUrl)
        {
            if (_categoriesCache.ContainsKey(repoUrl))
                return _categoriesCache[repoUrl];

            var categories = new List<CategoryData>();

            try
            {
                var apps = await GetAppsFromRepositoryAsync(repoUrl);
                var uniqueCategories = apps
                    .Where(a => !string.IsNullOrEmpty(a.Category))
                    .Select(a => a.Category)
                    .Distinct()
                    .ToList();

                int id = 0;
                foreach (var cat in uniqueCategories)
                {
                    categories.Add(new CategoryData { Id = id.ToString(), Name = cat });
                    id++;
                }

                _categoriesCache[repoUrl] = categories;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading categories: {ex.Message}");
            }

            return categories;
        }

        public async Task<List<AppItem>> GetAppsByCategoryAsync(string categoryKey)
        {
            var result = new List<AppItem>();

            foreach (var repo in _repositories)
            {
                var apps = await GetAppsFromRepositoryAsync(repo.Url);
                result.AddRange(apps.Where(a =>
                {
                    var appCategoryKey = a.Category?.ToLower() ?? string.Empty;
                    if (!ShouldShowApp(a))
                        return false;

                    if (categoryKey == OTHER_CATEGORY_KEY)
                    {
                        return !_categoryOrder.ContainsKey(appCategoryKey);
                    }
                    return appCategoryKey == categoryKey;
                }));
            }

            return result;
        }

        /// <summary>
        /// Поиск
        /// </summary>
        public async Task<List<AppItem>> SearchAppsAsync(string query)
        {
            var result = new List<AppItem>();
            var lowerQuery = query.ToLower();

            foreach (var repo in _repositories)
            {
                var apps = await GetAppsFromRepositoryAsync(repo.Url);
                result.AddRange(apps.Where(a =>
                {
                    if (!ShouldShowApp(a))
                        return false;

                    return a.Name.ToLower().Contains(lowerQuery) ||
                           a.Summary.ToLower().Contains(lowerQuery) ||
                           a.Description.ToLower().Contains(lowerQuery) ||
                           a.Publisher.ToLower().Contains(lowerQuery) ||
                           (a.AppType?.ToLower().Contains(lowerQuery) ?? false);
                }));
            }

            return result;
        }

        private async Task<List<AppItem>> GetAppsFromRepositoryAsync(string repoUrl)
        {
            if (_appsCache.ContainsKey(repoUrl))
                return _appsCache[repoUrl];

            var apps = new List<AppItem>();
            var indexUrl = GetIndexUrl(repoUrl);

            try
            {
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(new Uri(indexUrl));
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonText = await response.Content.ReadAsStringAsync();
                        var jsonObj = JsonObject.Parse(jsonText);

                        if (jsonObj.ContainsKey("apps"))
                        {
                            var appsArray = jsonObj.GetNamedArray("apps");
                            foreach (var jsonValue in appsArray)
                            {
                                try
                                {
                                    var appObj = jsonValue.GetObject();
                                    var app = ParseAppItem(appObj, repoUrl);
                                    if (ShouldShowApp(app))
                                    {
                                        apps.Add(app);
                                    }
                                }
                                catch { /*игнор*/ }
                            }
                        }
                        else if (jsonObj.ContainsKey("packages"))
                        {
                            var packagesObj = jsonObj.GetNamedObject("packages");
                            foreach (var packageId in packagesObj.Keys)
                            {
                                try
                                {
                                    var packageObj = packagesObj.GetNamedObject(packageId);
                                    var app = ParseAppItem(packageObj, repoUrl, packageId);
                                    if (ShouldShowApp(app))
                                    {
                                        apps.Add(app);
                                    }
                                }
                                catch { /*игнор*/ }
                            }
                        }

                        _appsCache[repoUrl] = apps;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading apps from {repoUrl}: {ex.Message}");
            }

            return apps;
        }

        private AppItem ParseAppItem(JsonObject jsonObj, string baseRepoUrl, string explicitId = null)
        {
            var appUrl = GetJsonString(jsonObj, "app_url");
            if (string.IsNullOrEmpty(appUrl))
            {
                appUrl = GetJsonString(jsonObj, "base_url");
            }

            var appUrlRoot = appUrl;
            if (!string.IsNullOrEmpty(appUrlRoot) && appUrlRoot.EndsWith("entry.json", StringComparison.OrdinalIgnoreCase))
            {
                appUrlRoot = appUrlRoot.Substring(0, appUrlRoot.Length - "entry.json".Length).TrimEnd('/');
            }

            var entryUrl = appUrl;
            if (!string.IsNullOrEmpty(entryUrl) && !entryUrl.EndsWith("entry.json", StringComparison.OrdinalIgnoreCase))
            {
                entryUrl = entryUrl.TrimEnd('/') + "/entry.json";
            }

            System.Diagnostics.Debug.WriteLine($"ParseAppItem: appUrl={appUrl}, appUrlRoot={appUrlRoot}, entryUrl={entryUrl}");

            var iconValue = GetJsonString(jsonObj, "icon");
            var iconUrl = string.Empty;

            if (!string.IsNullOrEmpty(iconValue))
            {
                iconUrl = CombineUrls(appUrl, iconValue);
            }

            var downloadUrl = GetJsonString(jsonObj, "download_url");
            var certificateUrl = GetJsonString(jsonObj, "cer_url");

            var summary = GetJsonString(jsonObj, "summary");
            if (string.IsNullOrEmpty(summary))
            {
                summary = GetJsonString(jsonObj, "description");
            }

            var appId = explicitId ?? GetJsonString(jsonObj, "id");
            if (string.IsNullOrEmpty(appId))
            {
                appId = GetJsonString(jsonObj, "title");
            }

            var app = new AppItem
            {
                Id = appId,
                Name = string.IsNullOrEmpty(GetJsonString(jsonObj, "title")) ? GetJsonString(jsonObj, "name") : GetJsonString(jsonObj, "title"),
                Summary = summary,
                Description = GetJsonString(jsonObj, "description"),
                Publisher = string.IsNullOrEmpty(GetJsonString(jsonObj, "author")) ? GetJsonString(jsonObj, "creator") : GetJsonString(jsonObj, "author"),
                Size = GetJsonString(jsonObj, "size"),
                Version = GetJsonString(jsonObj, "version"),
                Category = GetJsonString(jsonObj, "category"),
                BaseUrl = baseRepoUrl,
                AppUrl = appUrlRoot,
                EntryJsonUrl = !string.IsNullOrEmpty(entryUrl) ? CombineUrls(baseRepoUrl, entryUrl) : string.Empty,
                OS = GetJsonString(jsonObj, "os"),
                AppType = GetJsonString(jsonObj, "app_type"),
                Versions = new List<AppVersionInfo>(),
                Screenshots = ParseScreenshots(jsonObj, baseRepoUrl)
            };

            System.Diagnostics.Debug.WriteLine($"ParseAppItem: Final EntryJsonUrl={app.EntryJsonUrl}");

            if (!string.IsNullOrEmpty(iconUrl))
            {
                app.Icon = CombineUrls(baseRepoUrl, iconUrl);
            }

            if (!string.IsNullOrEmpty(downloadUrl))
            {
                app.DownloadUrl = CombineUrls(baseRepoUrl, downloadUrl);
            }

            if (!string.IsNullOrEmpty(certificateUrl))
            {
                app.CertificateUrl = CombineUrls(baseRepoUrl, certificateUrl);
            }

            app.MinVersion = GetJsonString(jsonObj, "min_ver");

            if (jsonObj.ContainsKey("versions"))
            {
                try
                {
                    var versionsArray = jsonObj.GetNamedArray("versions");
                    for (int i = 0; i < (int)versionsArray.Count; i++)
                    {
                        var versionObj = versionsArray[i].GetObject();
                        var versionText = GetJsonString(versionObj, "version");
                        var versionDownloadUrl = GetJsonString(versionObj, "download_url");
                        var appFile = GetJsonString(versionObj, "app_file");
                        if (string.IsNullOrEmpty(versionDownloadUrl) && !string.IsNullOrEmpty(appFile))
                        {
                            versionDownloadUrl = CombineUrls(appUrlRoot ?? baseRepoUrl, appFile);
                        }

                        if (!string.IsNullOrEmpty(versionText) && !string.IsNullOrEmpty(versionDownloadUrl))
                        {
                            app.Versions.Add(new AppVersionInfo
                            {
                                Version = versionText,
                                DownloadUrl = versionDownloadUrl
                            });
                        }
                    }

                    if (app.Versions.Count > 0)
                    {
                        app.Versions = app.Versions
                            .OrderByDescending(v => v.Version, Comparer<string>.Create(CompareVersionStrings))
                            .ToList();

                        var firstVersion = app.Versions[0];
                        app.Version = firstVersion.Version;
                        if (string.IsNullOrEmpty(app.DownloadUrl))
                        {
                            app.DownloadUrl = firstVersion.DownloadUrl;
                        }
                    }
                }
                catch { /*еще игнор*/ }
            }

            return app;
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

        private string GetJsonString(JsonObject obj, string key)
        {
            if (obj.ContainsKey(key))
            {
                var value = obj.GetNamedValue(key);
                if (value.ValueType == JsonValueType.String)
                    return value.GetString();
            }
            return string.Empty;
        }
        private string CombineUrls(string baseUrl, string relativePath)
        {
            if (!string.IsNullOrEmpty(relativePath) && 
                (relativePath.StartsWith("http://") || relativePath.StartsWith("https://") || relativePath.StartsWith("//")))
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
        private List<string> ParseScreenshots(JsonObject obj, string baseUrl)
        {
            var screenshots = new List<string>();
            if (obj.ContainsKey("screenshots"))
            {
                try
                {
                    var array = obj.GetNamedArray("screenshots");
                    foreach (var item in array)
                    {
                        if (item.ValueType == JsonValueType.String)
                        {
                            var screenshotUrl = item.GetString();
                            // Properly combine screenshot URL with base URL if it's relative
                            var fullUrl = CombineUrls(baseUrl, screenshotUrl);
                            screenshots.Add(fullUrl);
                        }
                    }
                }
                catch { }
            }
            return screenshots;
        }

        /// <summary>
        /// Сохранение репо в локальной памяти
        /// </summary>
        private async Task SaveRepositoriesAsync()
        {
            try
            {
                var jsonItems = new List<string>();
                foreach (var r in _repositories)
                {
                    var jsonStr = $"{{\"url\":\"{EscapeJson(r.Url)}\",\"name\":\"{EscapeJson(r.Name)}\",\"creator\":\"{EscapeJson(r.Creator)}\",\"lastUpdated\":\"{EscapeJson(r.LastUpdated)}\"}}";
                    jsonItems.Add(jsonStr);
                }
                var json = "[" + string.Join(",", jsonItems) + "]";

                var file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    "repositories.json", CreationCollisionOption.ReplaceExisting);

                await FileIO.WriteTextAsync(file, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving repositories: {ex.Message}");
            }
        }

        /// <summary>
        /// Загрузка репо оттуда
        /// </summary>
        private async Task LoadRepositoriesAsync()
        {
            try
            {
                var file = await ApplicationData.Current.LocalFolder.GetFileAsync("repositories.json");
                var json = await FileIO.ReadTextAsync(file);
                var array = JsonArray.Parse(json);

                _repositories.Clear();
                foreach (var item in array)
                {
                    try
                    {
                        var obj = item.GetObject();
                        _repositories.Add(new Repository
                        {
                            Url = GetJsonString(obj, "url"),
                            Name = GetJsonString(obj, "name"),
                            Creator = GetJsonString(obj, "creator"),
                            LastUpdated = GetJsonString(obj, "lastUpdated")
                        });
                    }
                    catch { }
                }
            }
            catch
            {
            }
        }

        private string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// Очиститб кеш + загрузить
        /// </summary>
        public async Task RefreshAsync()
        {
            _appsCache.Clear();
            _categoriesCache.Clear();
            await GetCategoriesAsync();
        }

        public async Task<AppItem> GetAppByIdAsync(string appId)
        {
            foreach (var repo in _repositories)
            {
                var apps = await GetAppsFromRepositoryAsync(repo.Url);
                var app = apps.FirstOrDefault(a => a.Id == appId);
                if (app != null)
                    return app;
            }
            return null;
        }

        private string NormalizeRepositoryRootUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            var normalized = url.Trim();
            if (normalized.EndsWith(INDEX_FILE_NAME, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - INDEX_FILE_NAME.Length);
            }

            if (normalized.EndsWith("properties.json", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring(0, normalized.Length - "properties.json".Length);
            }

            while (normalized.EndsWith("/"))
            {
                normalized = normalized.Substring(0, normalized.Length - 1);
            }

            return normalized;
        }

        private string GetIndexUrl(string repoRootUrl)
        {
            if (string.IsNullOrEmpty(repoRootUrl))
                return string.Empty;

            if (repoRootUrl.EndsWith("/"))
                return repoRootUrl + INDEX_FILE_NAME;

            return repoRootUrl + "/" + INDEX_FILE_NAME;
        }
    }
}

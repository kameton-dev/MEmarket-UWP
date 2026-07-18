using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace MEmarket_UWP.Models
{
    public static class UpdateChecker
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        public static List<UpdateableApp> CachedUpdates { get; set; } = new List<UpdateableApp>();

        public static async Task<List<UpdateableApp>> CheckForUpdatesAsync(List<TrackedApp> installedApps)
        {
            var updateableApps = new List<UpdateableApp>();
            
            var repoGroups = new Dictionary<string, List<TrackedApp>>();
            foreach (var app in installedApps)
            {
                if (string.IsNullOrEmpty(app.RepositoryUrl)) continue;

                string repoBaseUrl = app.RepositoryUrl.TrimEnd('/');
                if (!repoGroups.ContainsKey(repoBaseUrl))
                    repoGroups[repoBaseUrl] = new List<TrackedApp>();

                repoGroups[repoBaseUrl].Add(app);
            }
            
            foreach (var repo in repoGroups)
            {
                string repoUrl = repo.Key;
                var localApps = repo.Value;

                try
                {
                    string indexUrl = $"{repoUrl}/index.json";
                    string jsonText = await HttpClient.GetStringAsync(indexUrl);

                    if (JsonObject.TryParse(jsonText, out JsonObject root))
                    {
                        if (root.ContainsKey("packages"))
                        {
                            JsonObject packages = root.GetNamedObject("packages");

                            foreach (var localApp in localApps)
                            {
                                if (packages.ContainsKey(localApp.Id))
                                {
                                    JsonObject remoteApp = packages.GetNamedObject(localApp.Id);

                                    string latestVersion = remoteApp.ContainsKey("latest_version")
                                        ? remoteApp.GetNamedString("latest_version") : "0.0.0.0";

                                    string appUrl = remoteApp.ContainsKey("app_url")
                                        ? remoteApp.GetNamedString("app_url") : "";

                                    string iconFileName = remoteApp.ContainsKey("icon")
                                        ? remoteApp.GetNamedString("icon") : "icon.png";
                                    
                                    if (IsNewerVersion(localApp.InstalledVersion, latestVersion))
                                    {
                                        string absoluteIconUrl = appUrl + iconFileName;

                                        updateableApps.Add(new UpdateableApp
                                        {
                                            Id = localApp.Id,
                                            Name = localApp.Name,
                                            Icon = absoluteIconUrl,
                                            InstalledVersion = localApp.InstalledVersion,
                                            NewVersion = latestVersion,
                                            RepositoryUrl = localApp.RepositoryUrl,
                                            AppUrl = appUrl
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка проверки обновлений в {repoUrl}: {ex.Message}");
                }
            }

            return updateableApps;
        }

        private static bool IsNewerVersion(string currentVersion, string remoteVersion)
        {
            if (Version.TryParse(currentVersion, out Version local) &&
                Version.TryParse(remoteVersion, out Version remote))
            {
                return remote > local;
            }
            return string.Compare(remoteVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }
}

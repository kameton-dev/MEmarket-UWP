using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using MEmarket_UWP.Models;
using Windows.Storage;

namespace MEmarket_UWP.Services
{
    public static class LocalAppsManager
    {
        private const string FileName = "installed_apps.json";

        public static async Task SaveAppsAsync(List<TrackedApp> apps)
        {
            try
            {
                var jsonArray = new JsonArray();
                foreach (var app in apps)
                {
                    var appObject = new JsonObject();
                    appObject.Add("id", JsonValue.CreateStringValue(app.Id ?? ""));
                    appObject.Add("name", JsonValue.CreateStringValue(app.Name ?? ""));
                    appObject.Add("icon", JsonValue.CreateStringValue(app.Icon ?? ""));
                    appObject.Add("version", JsonValue.CreateStringValue(app.InstalledVersion ?? ""));
                    appObject.Add("repoUrl", JsonValue.CreateStringValue(app.RepositoryUrl ?? ""));
                    appObject.Add("appUrl", JsonValue.CreateStringValue(app.AppUrl ?? "")); // Новое поле
                    appObject.Add("installDate", JsonValue.CreateStringValue(app.InstallDate.ToString("o")));

                    jsonArray.Add(appObject);
                }

                StorageFolder folder = ApplicationData.Current.LocalFolder;
                StorageFile file = await folder.CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(file, jsonArray.Stringify());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения: {ex.Message}");
            }
        }

        public static async Task<List<TrackedApp>> LoadAppsAsync()
        {
            var list = new List<TrackedApp>();
            StorageFolder folder = ApplicationData.Current.LocalFolder;
            try
            {
                StorageFile file = await folder.GetFileAsync(FileName);
                string jsonText = await FileIO.ReadTextAsync(file);

                if (JsonArray.TryParse(jsonText, out JsonArray jsonArray))
                {
                    foreach (var value in jsonArray)
                    {
                        var appObject = value.GetObject();
                        var app = new TrackedApp
                        {
                            Id = appObject.ContainsKey("id") ? appObject.GetNamedString("id") : "",
                            Name = appObject.ContainsKey("name") ? appObject.GetNamedString("name") : "",
                            Icon = appObject.ContainsKey("icon") ? appObject.GetNamedString("icon") : "",
                            InstalledVersion = appObject.ContainsKey("version") ? appObject.GetNamedString("version") : "",
                            RepositoryUrl = appObject.ContainsKey("repoUrl") ? appObject.GetNamedString("repoUrl") : "",
                            AppUrl = appObject.ContainsKey("appUrl") ? appObject.GetNamedString("appUrl") : ""
                        };

                        if (appObject.ContainsKey("installDate") && DateTime.TryParse(appObject.GetNamedString("installDate"), out DateTime date))
                        {
                            app.InstallDate = date;
                        }
                        list.Add(app);
                    }
                }
            }
            catch (System.IO.FileNotFoundException) { }
            return list;
        }
        
        public static async Task RegisterInstalledAppAsync(string id, string name, string icon, string version, string repoUrl, string appUrl)
        {
            var apps = await LoadAppsAsync();
            apps.RemoveAll(a => a.Id == id);

            apps.Add(new TrackedApp
            {
                Id = id,
                Name = name,
                Icon = icon,
                InstalledVersion = version,
                RepositoryUrl = repoUrl,
                AppUrl = appUrl,
                InstallDate = DateTime.Now
            });

            await SaveAppsAsync(apps);
        }
    }
}

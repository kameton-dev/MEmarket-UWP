using System;

namespace MEmarket_UWP
{
    public class TrackedApp
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public string InstalledVersion { get; set; }
        public string RepositoryUrl { get; set; }
        public string AppUrl { get; set; }
        public DateTime InstallDate { get; set; }

        public string InstalledVersionText => $"Версия: {InstalledVersion}";
    }

    public class UpdateableApp : TrackedApp
    {
        public string NewVersion { get; set; }
        public string VersionProgress => $"{InstalledVersion} ➔ {NewVersion}";
    }
}
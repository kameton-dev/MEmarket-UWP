using System.Collections.Generic;

namespace MEmarket_UWP.Models
{
    public class AppItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string Publisher { get; set; }
        public string DownloadUrl { get; set; }
        public string CertificateUrl { get; set; }
        public string Size { get; set; }
        public string Version { get; set; }
        public string MinVersion { get; set; }
        public string Category { get; set; }
        public string BaseUrl { get; set; }
        public string OS { get; set; }
        public List<AppVersionInfo> Versions { get; set; } = new List<AppVersionInfo>();
        public List<string> Screenshots { get; set; } = new List<string>();
    }

    public class AppVersionInfo
    {
        public string Version { get; set; }
        public string DownloadUrl { get; set; }
    }
}

using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace taiyuanhitech.TGFCSpiderman.OnlineUpdate
{
    sealed class UpdateInfo
    {
        public Version NewVersion { get; set; }
        public string DownloadUrl { get; set; }
        public string Desctription { get; set; }
    }
    sealed class OnlineUpdateManager
    {
        const string LatestReleaseUrl = "http://localhost/lastest_release.txt";
        public static Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }
        public async Task<UpdateInfo> GetUpdateInfo()
        {
            using (var httpClient = new HttpClient())
            {
                var updateInfoStream = await httpClient.GetStreamAsync(LatestReleaseUrl);
                using (var reader = new StreamReader(updateInfoStream))
                {
                    var lineIndex = 0;
                    var update = new UpdateInfo();
                    while (!reader.EndOfStream)
                    {
                        switch (lineIndex)
                        {
                            case 0:
                                // ReSharper disable once AssignNullToNotNullAttribute
                                var newVersion = new Version(reader.ReadLine());
                                if (newVersion <= GetCurrentVersion())
                                {
                                    return null;
                                }
                                update.NewVersion = newVersion;
                                break;
                            case 1:
                                update.DownloadUrl = reader.ReadLine();
                                break;
                            default:
                                update.Desctription = reader.ReadToEnd();
                                break;
                        }
                        lineIndex++;
                    }
                    return update;
                }
            }
        }
    }
}

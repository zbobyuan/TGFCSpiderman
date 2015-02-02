using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using taiyuanhitech.TGFCSpiderman.Configuration;

namespace taiyuanhitech.TGFCSpiderman.OnlineUpdate
{
    sealed class UpdateInfo
    {
        public Version NewVersion { get; set; }
        public int UpdatePackageSize { get; set; }
        public string DownloadUrl { get; set; }

        public string Desctription { get; set; }
    }
    sealed class OnlineUpdateManager
    {
        public static Version GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version;
        }
        public static async Task<UpdateInfo> GetUpdateInfoAsync()
        {
            using (var httpClient = new HttpClient())
            {
                var updateInfoStream = await httpClient.GetStreamAsync(GetUpdateUrl());
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
                                    return null;
                                update.NewVersion = newVersion;
                                break;
                            case 1:
                                update.UpdatePackageSize = int.Parse(reader.ReadLine());
                                break;
                            case 2:
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

        public static async Task DownLoadUpdatePackageAsync(UpdateInfo updateInfo, Stream outputStream, IProgress<int> progress)
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.ExpectContinue = false;
            var response = await httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                var totalReaded = 0;
                int bytesReaded;
                var buf = new byte[1024*4];
                var prevPercentage = 0;
                while((bytesReaded = await stream.ReadAsync(buf, 0, buf.Length)) > 0){
                    await outputStream.WriteAsync(buf, 0, bytesReaded);
                    totalReaded += bytesReaded;
                    var completedPercentage = (int)((double)totalReaded / updateInfo.UpdatePackageSize * 100);
                    if (completedPercentage > prevPercentage)
                    {
                        prevPercentage = completedPercentage;
                        progress.Report(completedPercentage);
                    }
                }
            }
        }

        private static string GetUpdateUrl()
        {
            return new ConfigurationManager().GetOnlineUpdateConfig().CheckUpdateUrl;
        }
    }
}

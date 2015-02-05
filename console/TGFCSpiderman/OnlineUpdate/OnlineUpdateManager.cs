using Ionic.Zip;
using System;
using System.IO;
using System.Linq;
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

    internal sealed class DownloadProgress
    {
        public DownloadProgress(int p, string status)
        {
            CompletedPercentage = p;
            Status = status;
        }

        public int CompletedPercentage { get; set; }
        public string Status { get; set; }
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

        public static async Task SetupUpdateAsync(UpdateInfo updateInfo, IProgress<DownloadProgress> progress)
        {
            var saveFolder = Path.Combine(Path.GetTempPath(), "TGFCSpidermanUpdate");
            if (!Directory.Exists(saveFolder))
            {
                Directory.CreateDirectory(saveFolder);
            }

            var saveFileName = Path.Combine(saveFolder, Path.GetTempFileName());
            using (var fileStream = File.OpenWrite(saveFileName))
            {
                await DownLoadUpdatePackageAsync(updateInfo, fileStream, progress);
            }
            progress.Report(new DownloadProgress(90, "正在解压"));
            //TODO:解压之前确认下载的文件是合法有效的，防止黑客篡改导致用户受损。使用RSA，发布前必须实现。
            var extractFolder = Path.Combine(saveFolder, Path.GetRandomFileName());
            await Task.Run(() =>
            {
                Directory.CreateDirectory(extractFolder);
                using (var zip = ZipFile.Read(saveFileName))
                {
                    zip.ExtractAll(extractFolder);
                }
                File.Delete(saveFileName);
            });
            progress.Report(new DownloadProgress(95, "准备更新"));
            var sourceUpdater = Path.Combine(extractFolder, "TGSUpdater.exe");

            if (File.Exists(sourceUpdater))
            {//用TGFCSpiderman更新updater，再用updater更新TGFCSpiderman
                File.Copy(sourceUpdater, Path.Combine(Directory.GetCurrentDirectory(), "TGSUpdater.exe"), true);
            }
            new ConfigurationManager().SavePendingUpdate(extractFolder, updateInfo.NewVersion.ToString());
            progress.Report(new DownloadProgress(100, ""));
        }

        public static async Task DownLoadUpdatePackageAsync(UpdateInfo updateInfo, Stream outputStream, IProgress<DownloadProgress> progress)
        {
            progress.Report(new DownloadProgress(0, "正在连接"));
            var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            var contentLengthStr = response.Content.Headers.GetValues("Content-Length").FirstOrDefault();
            if (!string.IsNullOrEmpty(contentLengthStr))
            {
                int sizeFromServer;
                if (int.TryParse(contentLengthStr, out sizeFromServer) && sizeFromServer != updateInfo.UpdatePackageSize)
                {
                    updateInfo.UpdatePackageSize = sizeFromServer;
                }
            }
            progress.Report(new DownloadProgress(5, "正在下载"));
            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                var totalReaded = 0;
                int bytesReaded;
                var buf = new byte[1024*4];
                var prevPercentage = 5;
                var downloadProgress = new DownloadProgress(0, "正在下载");
                while((bytesReaded = await stream.ReadAsync(buf, 0, buf.Length)) > 0){
                    await outputStream.WriteAsync(buf, 0, bytesReaded);
                    totalReaded += bytesReaded;
                    var completedPercentage = (int)((double)totalReaded / updateInfo.UpdatePackageSize * 100 * .85);
                    if (completedPercentage > prevPercentage)
                    {
                        prevPercentage = completedPercentage;
                        downloadProgress.CompletedPercentage = completedPercentage;
                        progress.Report(downloadProgress);
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

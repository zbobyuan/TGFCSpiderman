using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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
        public string UpdatePackageHash { get; set; }
        public string Description { get; set; }
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
                var response = await httpClient.GetStringAsync(GetUpdateUrl());
                var updateInfoAndSignature = Regex.Split(response, @"(?:\r\n){2}");
                var updateInfo = updateInfoAndSignature[0];
                var signature = updateInfoAndSignature[1];

                return VerifySignature(updateInfo, signature) ? GetUpdateInfoFromText(updateInfo) : null;
            }
        }

        public static bool VerifySignature(string text, string signature)
        {
            string publicKeyXml;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("taiyuanhitech.TGFCSpiderman.pk.xml"))
            using (var reader = new StreamReader(stream))
            {
                publicKeyXml = reader.ReadToEnd();
            }
            var csp = new RSACryptoServiceProvider();
            csp.FromXmlString(publicKeyXml);

            var data = Encoding.UTF8.GetBytes(text);
            var hash = new SHA1Managed().ComputeHash(data);
            return csp.VerifyHash(hash, CryptoConfig.MapNameToOID("SHA1"), StringToByteArray(signature));
        }

        public static UpdateInfo GetUpdateInfoFromText(string text)
        {
            var update = new UpdateInfo();
            using (var reader = new StringReader(text))
            {
                for (var i = 0; ; i++)
                {
                    switch (i)
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
                        case 3:
                            update.UpdatePackageHash = reader.ReadLine();
                            break;
                        default:
                            update.Description = reader.ReadToEnd();
                            return update;
                    }
                }
            }
        }

        private static string ByteArrayToString(byte[] buffer)
        {
            var sb = new StringBuilder();
            foreach (var b in buffer)
                sb.Append(b.ToString("X2"));

            return (sb.ToString());
        }

        private static byte[] StringToByteArray(string hex)
        {
            hex = hex.Trim();
            var numberChars = hex.Length;
            var bytes = new byte[numberChars / 2];
            for (var i = 0; i < numberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);

            return bytes;
        }
        public static byte[] ComputeHash(string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                return new SHA256CryptoServiceProvider().ComputeHash(fs);
            }
        }

        public static bool VerifyHash(string fileName, string hash)
        {
            var newHash = ByteArrayToString(ComputeHash(fileName));
            return newHash == hash;
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
            progress.Report(new DownloadProgress(80, "正在验证"));
            if (!VerifyHash(saveFileName, updateInfo.UpdatePackageHash))
            {
                throw new Exception("验证错误");
            }
            progress.Report(new DownloadProgress(90, "正在解压"));
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

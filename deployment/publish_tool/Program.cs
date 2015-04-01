using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace publish_tool
{
    class Program
    {
        static void Main(string[] args)
        {
            var version = new Version(args[0]);
            var updateBallPath = args[1];
            var fileToOpen = Regex.Replace(Path.GetTempFileName(), "tmp$", "txt");
            using (var fileStream = File.OpenWrite(fileToOpen))
            {
                using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                {
                    writer.WriteLine("#更新内容：");
                }
            }
            var process = new Process
            {
                StartInfo = new ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = fileToOpen
                }
            };

            process.Start();
            process.WaitForExit();

            var updateInfo = File.ReadAllLines(fileToOpen, Encoding.UTF8);
            var updateBall = new FileInfo(updateBallPath);
            var fileSize = updateBall.Length;
            var updateInfoFileName = Path.Combine(updateBall.DirectoryName, "lastest_release.txt");

            var sb = new StringBuilder();
            sb.AppendLine(version.ToString());
            sb.AppendLine(fileSize.ToString());
            sb.AppendFormat("https://github.com/zbobyuan/TGFCSpiderman/releases/download/r_{0}.{1}/{2}{3}", version.Major, version.Minor, updateBall.Name, Environment.NewLine);
            sb.AppendLine(ByteArrayToString(ComputeHash(updateBallPath)));
            foreach (var u in updateInfo)
            {
                if (!u.StartsWith("#"))
                    sb.AppendLine(u);
            }

            var parms = new CspParameters(1)
            {
                Flags = CspProviderFlags.UseMachineKeyStore,
                KeyContainerName = "TGFCSpiderman",
                KeyNumber = 2
            };
            var csp = new RSACryptoServiceProvider(parms);
            var text = sb.ToString().TrimEnd();
            var data = Encoding.UTF8.GetBytes(text);
            var hash = new SHA1Managed().ComputeHash(data);
            var resultBytes = csp.SignHash(hash, CryptoConfig.MapNameToOID("SHA1"));
            var result = ByteArrayToString(resultBytes);

            sb.AppendLine();
            sb.Append(result);

            using (var fileStream = File.OpenWrite(updateInfoFileName))
            {
                using (var writer = new StreamWriter(fileStream, Encoding.UTF8))
                {
                    writer.Write(sb.ToString());
                }
            }
        }

        public static byte[] ComputeHash(string fileName)
        {
            using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                return new SHA256CryptoServiceProvider().ComputeHash(fs);
            }
        }

        private static string ByteArrayToString(byte[] buffer)
        {
            var sb = new StringBuilder();
            foreach (var b in buffer)
                sb.Append(b.ToString("X2"));

            return (sb.ToString());
        }
    }
}

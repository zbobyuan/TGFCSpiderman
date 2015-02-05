using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using SQLite;

namespace taiyuanhitech.Updater
{
    public class UpdateException : Exception
    {
        public UpdateException(string message)
            : base(message)
        {
        }
    }

    public class VersionChecker : MarshalByRefObject
    {
        public bool VersionMatched(string mainAppFilePath, string targetVersion)
        {
            var asm = Assembly.ReflectionOnlyLoadFrom(mainAppFilePath);
            return asm.GetName().Version.ToString() == targetVersion;
        }
    }

    public partial class MainWindow
    {
        private readonly BackgroundWorker _worker = new BackgroundWorker();
        private readonly App _app = (App)Application.Current;
        private readonly StringBuilder _logBuilder = new StringBuilder();
        public MainWindow()
        {
            InitializeComponent();
            _worker.DoWork += WorkerOnDoWork;
            _worker.RunWorkerCompleted += WorkerOnRunWorkerCompleted;
            _worker.WorkerReportsProgress = true;
            _worker.ProgressChanged += WorkerOnProgressChanged;
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            _worker.RunWorkerAsync();
        }

        /*更新步骤
             * 1、判断传入的参数，是否存在文件夹，文件夹是否存在文件，文件的版本是否与传入的ver相同
             * 2、等待主程序退出
             * 3、如果存在数据库更新，备份数据库
             * 4、如果存在数据库更新，执行sql
             * 5、更新文件
             * 6、删除更新源
             */
        private void WorkerOnDoWork(object sender, DoWorkEventArgs doWorkEventArgs)
        {
            PrepareUpdate();
            CheckVersion();
            WaitForMainAppExit();
            UpdateDatabase();
            UpdateFiles();
        }

        private void WorkerOnRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs runWorkerCompletedEventArgs)
        {
            if (runWorkerCompletedEventArgs.Error != null)
            {
                Log("ERROR", runWorkerCompletedEventArgs.Error);
                using (var stream = File.Open("updatelog.txt", FileMode.Append))
                {
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.Write(_logBuilder.ToString());
                    }

                }
                UpdateStatus.Foreground = Brushes.Red;
                UpdateStatus.Text = "更新过程发生错误，打来 updatelog.txt 查看详情。";
                return;
            }
            UpdateStatus.Text = "更新完成,即将启动TGFCSpiderman。";
            UpdateProgress.Value = 100;
            Thread.Sleep(500);
            Process.Start("TGFCSpiderman.exe");
            Close();
        }
        private void WorkerOnProgressChanged(object sender, ProgressChangedEventArgs progressChangedEventArgs)
        {
            UpdateProgress.Value = progressChangedEventArgs.ProgressPercentage;
            UpdateStatus.Text = (string)(progressChangedEventArgs.UserState);
        }

        private void PrepareUpdate()
        {
            _worker.ReportProgress(0, "准备更新");
            Log("=========================================================================");
            Log(string.Format("准备更新,Dir:{0},Ver:{1}", _app.Dir, _app.Ver));
            Thread.Sleep(500);
            var updateFilesDir = _app.Dir;

            if (!Directory.Exists(updateFilesDir) || Directory.GetFiles(updateFilesDir).Length == 0)
            {
                throw new UpdateException("找不到更新文件，不能更新。");
            }
            var mainAppFile = Path.Combine(updateFilesDir, "TGFCSpiderman.exe");
            if (!File.Exists(mainAppFile))
            {
                throw new UpdateException("找不到更新文件，不能更新。");
            }
        }

        private void CheckVersion()
        {
            _worker.ReportProgress(5, "检查版本信息");
            Log("检查版本信息");
            var mainAppFilePath = Path.Combine(_app.Dir, "TGFCSpiderman.exe");
            _worker.ReportProgress(5, "检查版本信息");
            var domain = AppDomain.CreateDomain("dom");
            var checker = (VersionChecker)domain.CreateInstanceAndUnwrap(Assembly.GetExecutingAssembly().FullName, typeof(VersionChecker).FullName);
            try
            {
                if (!checker.VersionMatched(mainAppFilePath, _app.Ver))
                    throw new UpdateException("版本不一致，不能更新。");
            }
            finally
            {
                AppDomain.Unload(domain);
            }
        }

        private void WaitForMainAppExit()
        {
            _worker.ReportProgress(20, "等待主程序退出");
            Log("等待主程序退出");
            Thread.Sleep(500);

            var mainAppExited = false;
            Mutex m = null;
            try
            {
                m = Mutex.OpenExisting("[E0F06A0A-1399-4E86-ACC6-C48973F2B854}");
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                mainAppExited = true;
            }

            if (!mainAppExited)
            {
                try
                {
                    m.WaitOne();
                }
                catch (AbandonedMutexException)
                {
                }
            }
        }

        private void UpdateDatabase()
        {
            _worker.ReportProgress(30, "检查数据库更新");
            Log("检查数据库更新");
            Thread.Sleep(500);
            var sqlFileName = Path.Combine(_app.Dir, "update.sql");
            if (File.Exists(sqlFileName))
            {
                const string dbName = "tgfc.sqlite";
                const string dbBak = dbName + ".bak";
                Log("备份数据库");
                _worker.ReportProgress(40, "备份数据库");
                File.Copy(dbName, dbBak, true);
                Log("更新数据库");
                _worker.ReportProgress(60, "更新数据库");
                var sqls = File.ReadAllLines(sqlFileName);
                using (var conn = new SQLiteConnection(dbName, true))
                {
                    foreach (var sql in sqls.Where(sql => !string.IsNullOrEmpty(sql)))
                    {
                        conn.Execute(sql);
                    }
                }
                Log("删除备份数据库");
                _worker.ReportProgress(80, "删除备份数据库");
                File.Delete(dbBak);
            }
        }

        private void UpdateFiles()
        {
            Log("更新文件");
            _worker.ReportProgress(90, "更新文件");
            var files = Directory.GetFiles(_app.Dir);
            var currentPath = Directory.GetCurrentDirectory();
            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                if (NeedCopy(fileName))
                {
                    File.Copy(file, Path.Combine(currentPath, fileName), true);
                }
            }
            Directory.Delete(_app.Dir, true);
        }

        private void Log(string message, Exception e = null)
        {
            _logBuilder.AppendFormat("[{0:yyyy-MM-dd HH:mm:ss.fff}]:{1}{2}{3}{4}", DateTime.Now, message, Environment.NewLine, e, Environment.NewLine);
        }

        private static bool NeedCopy(string fileName)
        {
            string[] bypasses = { "TGXUpdater.exe", "update.sql" };
            return !bypasses.Any(b => b.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
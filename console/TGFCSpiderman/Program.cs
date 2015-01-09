using System;
using System.Collections.Generic;
using System.Linq;
using taiyuanhitech.TGFCSpiderman.Configuration;
using taiyuanhitech.TGFCSpiderman.Persistence;

namespace taiyuanhitech.TGFCSpiderman
{
    internal class Program
    {
        class AuthConfig : IAuthConfig
        {
            public string UserName { get; set; }
            public string AuthToken { get; set; }
        }

        private static string _userName;
        private static string _password;
        private static void Main(string[] args)
        {
            ComponentFactory.Startup();

            var configurationManager = new ConfigurationManager();
            configurationManager.SavePageFetcherConfig(configurationManager.GetPageFetcherConfig());
            var authConfig = configurationManager.GetAuthConfig();
            _userName = authConfig.UserName;
            _password = authConfig.AuthToken;

            bool signedIn = false, saveAuthInfo = false;
            while (!signedIn)
            {
                if (string.IsNullOrEmpty(_userName) || string.IsNullOrEmpty(_password))
                {
                    AskUserName();
                    AskPassword();
                    saveAuthInfo = true;
                }
                try
                {
                    ComponentFactory.GetPageFetcher().Signin(_userName, _password).Wait();
                    signedIn = true;
                    if (saveAuthInfo)
                        configurationManager.SaveAuthConfig(new AuthConfig {UserName = _userName, AuthToken = _password});
                }
                catch (AggregateException ae)
                {
                    var inner = ae.InnerException;
                    if (inner is CannotSigninException)
                    {
                        if (string.IsNullOrEmpty(inner.Message))
                        {
                            Console.WriteLine("无法登陆，按任意键退出本系统并检查网络后重试。");
                            Console.ReadKey();
                            return;
                        }
                        else
                        {
                            Console.WriteLine(inner.Message);
                            AskUserName();
                            AskPassword();
                            saveAuthInfo = true;
                        }
                    }
                }
            }

            Console.WriteLine("开始执行...");
            TaskQueueManager.Inst.Run(_userName, _password, DateTime.Now.AddDays(-1));

            Console.WriteLine("跑完了.{0}按任意键退出。", Environment.NewLine);
            Console.ReadKey();
        }

        private static void AskUserName()
        {
            Console.WriteLine("请输入用户名并按回车：");
            _userName = Console.ReadLine();
        }

        private static void AskPassword()
        {
            Console.WriteLine("请输入密码并按回车：");
            _password = Console2.ReadPassword();
        }
    }

    #region ReadPassword Console
    /// <summary>
    /// Adds some nice help to the console. Static extension methods don't exist (probably for a good reason) so the next best thing is congruent naming.
    /// Cut from http://stackoverflow.com/questions/3404421/password-masking-console-application
    /// Thanks shermy.
    /// </summary>
    static public class Console2
    {
        /// <summary>
        /// Like System.Console.ReadLine(), only with a mask.
        /// </summary>
        /// <param name="mask">a <c>char</c> representing your choice of console mask</param>
        /// <returns>the string the user typed in </returns>
        public static string ReadPassword(char mask)
        {
            const int ENTER = 13, BACKSP = 8, CTRLBACKSP = 127;
            int[] FILTERED = { 0, 27, 9, 10 /*, 32 space, if you care */ }; // const

            var pass = new Stack<char>();
            char chr = (char)0;

            while ((chr = System.Console.ReadKey(true).KeyChar) != ENTER)
            {
                if (chr == BACKSP)
                {
                    if (pass.Count > 0)
                    {
                        System.Console.Write("\b \b");
                        pass.Pop();
                    }
                }
                else if (chr == CTRLBACKSP)
                {
                    while (pass.Count > 0)
                    {
                        System.Console.Write("\b \b");
                        pass.Pop();
                    }
                }
                else if (FILTERED.Count(x => chr == x) > 0) { }
                else
                {
                    pass.Push((char)chr);
                    System.Console.Write(mask);
                }
            }

            System.Console.WriteLine();

            return new string(pass.Reverse().ToArray());
        }

        /// <summary>
        /// Like System.Console.ReadLine(), only with a mask.
        /// </summary>
        /// <returns>the string the user typed in </returns>
        public static string ReadPassword()
        {
            return ReadPassword('*');
        }
    }
    #endregion
}
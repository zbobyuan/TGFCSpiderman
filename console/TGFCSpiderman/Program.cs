using System;
using System.Collections.Generic;
using System.Linq;

namespace taiyuanhitech.TGFCSpiderman
{
    internal class Program
    {
        private static string _userName;
        private static string _password;
        private static void Main(string[] args)
        {
            AskUserName();
            AskPassword();

            ComponentFactory.Startup();
            bool signedIn = false;

            while (!signedIn)
            {
                try
                {
                    ComponentFactory.GetPageFetcher().Signin(_userName, _password);
                    signedIn = true;
                }
                catch (UserNameOrPasswordException)
                {
                    Console.WriteLine("用户名或密码错误，请重新输入。");
                    AskUserName();
                    AskPassword();
                }
                catch (CannotSigninException)
                {
                    Console.WriteLine("无法登陆，按任意键退出本系统并检查网络后重试。");
                    Console.ReadKey();
                    return;
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
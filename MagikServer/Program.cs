using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic;
using Microsoft.Contests.Bop.Participants.Magik.Analysis;
using Microsoft.Owin.Hosting;
using Newtonsoft.Json;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikServer
{
    /// <summary>
    /// 为远程计算机提供 MAGIK Web 服务。
    /// </summary>
    class Program
    {
        /// <summary>
        /// 主入口。
        /// </summary>
        static void Main(string[] args)
        {
            EnableProfiling();
            //ForceJit(typeof (Analyzer).Assembly);
            //ForceJit(typeof (JsonConverter).Assembly);
            // 载入设置
            GlobalServices.ASUseUltimateKey = Configurations.ASClientUseUltimateKey;
            // 选择入口点。
            if (Environment.UserInteractive)
                ConsoleMain();
            else
                ServiceMain();
        }

        /// <summary>
        /// 控制台的主入口。
        /// </summary>
        /// <param name="args"></param>
        static void ConsoleMain()
        {
            Console.WriteLine(Configurations.DumpConfigurations());
            Console.WriteLine(Utility.ProductName);
            Console.WriteLine(Utility.ApplicationTitle + " " + Utility.ProductVersion);
            // 开始预热。
            var warmUpTask = Analyzer.WarmUpAsync(AnalyzerFactory.CreateSearchClient());
            warmUpTask = warmUpTask.ContinueWith(t => warmUpTask = null);
            // Start OWIN host 
            using (StartWebApp())
            {
                WAITFORKEY:
                Console.WriteLine("请按任意键以结束服务。");
                Console.ReadKey(true);
                Console.WriteLine("请键入 EXIT 并回车以结束服务。");
                if (Console.ReadLine() != "EXIT") goto WAITFORKEY;
            }
        }

        /// <summary>
        /// 服务的主入口。
        /// </summary>
        static void ServiceMain()
        {
            var service = new MagikWebService();
            ServiceBase.Run(service);
        }

        /// <summary>
        /// 用于根据配置，启动网络服务。
        /// </summary>
        public static IDisposable StartWebApp()
        {
            var options = new StartOptions();
            foreach (var ba in Configurations.BaseAddresses) options.Urls.Add(ba);
            return WebApp.Start<Startup>(options);
        }

        /// <summary>
        /// 为应用程序启用 Multicore JIT 编译优化。
        /// </summary>
        private static void EnableProfiling()
        {
            ProfileOptimization.SetProfileRoot(Utility.ApplicationLocation);
            // 读取/覆盖 Profile 。
            ProfileOptimization.StartProfile("MagikServer.Startup.Profile");
        }

        /// <summary>
        /// 确保程序集中的函数已经被 JIT 编译。
        /// https://blogs.msdn.microsoft.com/abhinaba/2014/09/29/net-just-in-time-compilation-and-warming-up-your-system/
        /// </summary>
        /// <param name="assembly"></param>
        private static void ForceJit(Assembly assembly)
        {
            var types = assembly.GetTypes();
            foreach (var t in types)
                ForceJit(t);
        }

        private static void ForceJit(Type type)
        {
            var ctors = type.GetConstructors(BindingFlags.NonPublic
                                        | BindingFlags.Public
                                        | BindingFlags.Instance
                                        | BindingFlags.Static);
            foreach (var ctor in ctors) JitMethod(ctor);
            var methods = type.GetMethods(BindingFlags.DeclaredOnly
                                    | BindingFlags.NonPublic
                                    | BindingFlags.Public
                                    | BindingFlags.Instance
                                    | BindingFlags.Static);
            foreach (var method in methods) JitMethod(method);
        }

        private static void JitMethod(MethodBase method)
        {
            if (method.IsAbstract || method.ContainsGenericParameters)
                return;
            RuntimeHelpers.PrepareMethod(method.MethodHandle);
        }
    }
}

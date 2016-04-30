using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic;
using Microsoft.Contests.Bop.Participants.Magik.Analysis;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikConsole
{
    /// <summary>
    /// 一个用于在本地进行交互的 MAGIK 查询控制台。
    /// </summary>
    class Program
    {
        internal static void Main(string[] args)
        {
            try
            {
                Task.WaitAll(MainAsync());
            }
            catch (AggregateException ex)
            {
                // 展开内部异常。
                if (ex.InnerExceptions.Count == 1)
                    ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
                throw;
            }
        }

        private static async Task MainAsync()
        {
            var analyzer = new Analyzer();
            var isFirstTime = true;
            while (true)
            {
                Console.WriteLine("请键入两个实体/作者的 Id，使用空格分隔。");
                if (isFirstTime)
                {
                    isFirstTime = false;
                    Console.WriteLine("例如：  2157025439 2061503185");
                }
                var inp = Console.ReadLine()?.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (inp == null || inp.Length == 0)
                {
                    Console.WriteLine("再见！");
                    return;
                }
                if (inp.Length < 2)
                {
                    Console.WriteLine("参数数量不足。");
                    continue;
                }
                try
                {
                    var ids = inp.Take(2).Select(s => Convert.ToInt64(s)).ToArray();
                    Console.WriteLine("请稍后……");
                    var sw = Stopwatch.StartNew();
                    var paths = await analyzer.FindPathsAsync(ids[0], ids[1]);
                    sw.Stop();
                    Console.WriteLine("找到{0}条路径，用时{1}。", paths.Count, sw.Elapsed);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex.Message);
                }
            }
        }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic;
using Microsoft.Contests.Bop.Participants.Magik.Analysis;
using SEB = Microsoft.Contests.Bop.Participants.Magik.Academic.SearchExpressionBuilder;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikConsole
{
    /// <summary>
    /// 一个用于在本地进行交互的 MAGIK 查询控制台。
    /// </summary>
    static class Program
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
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine("查找联系：请键入两个实体/作者的 Id，使用空格和/或逗号分隔。");
                Console.WriteLine("例如： 2157025439 2061503185");
                //Console.WriteLine("查找论文：键入一个实体/作者的名称或 Id 。将会选取最匹配的结果显示。");
                Console.Write(" >");
                var inp = Console.ReadLine()?.Split(new[] {' ', '\t', ',', '[', ']'},
                    StringSplitOptions.RemoveEmptyEntries);
                if (inp == null || inp.Length == 0)
                {
                    Console.WriteLine("再见！");
                    return;
                }
                try
                {
                    if (inp.Any(f => f.Any(c => !char.IsNumber(c))))
                    {
                        // 存在非数字内容。
                        // 检索文献/作者。
                        var name = string.Join(" ", inp).ToLowerInvariant();
                        await FindEntityAuthorAsync(name);
                    }
                    else
                    {
                        var ids = inp.Take(2).Select(s => Convert.ToInt64(s)).ToArray();
                        if (ids.Length == 1) await FindEntityAuthorAsync(ids[0]);
                        else await FindPathsAsync(ids[0], ids[1]);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ExpandErrorMessage(ex));
                }
            }
        }

        private static async Task FindEntityAuthorAsync(string normalizedName)
        {
            throw new NotImplementedException();
        }

        private static async Task FindEntityAuthorAsync(long id)
        {
            throw new NotImplementedException();
        }

        private static async Task FindPathsAsync(long id1, long id2)
        {
            // 消除本地缓存对性能的影响。
            var analyzer = new Analyzer(GlobalServices.CreateASClient());
            Console.WriteLine("请稍后……");
            var sw = Stopwatch.StartNew();
            var paths = await analyzer.FindPathsAsync(id1, id2);
            sw.Stop();
            Console.WriteLine("找到 {0} 条路径，用时 {1} 。", paths.Length, sw.Elapsed);
            foreach (var g in paths.ToLookup(p => p.Length).OrderBy(g1 => g1.Key))
            {
                Console.WriteLine("{0}-hop：{1} 条。", g.Key - 1, g.Count());
            }
        }

        /// <summary>
        /// 展开异常消息，以避免出现诸如“发生一个或多个错误”这样无用的消息。
        /// </summary>
        public static string ExpandErrorMessage(Exception ex)
        {
            if (ex == null) throw new ArgumentNullException(nameof(ex));
            var agg = ex as AggregateException;
            if (agg != null)
                return string.Join(";", agg.InnerExceptions.Select(ExpandErrorMessage));
            return $"{ex.GetType().Name}:ex.Message";
        }
    }
}

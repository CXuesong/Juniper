using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Contests.Bop.Participants.Magik.Analysis;

namespace Microsoft.Contests.Bop.Participants.Magik
{
    internal static class EventId
    {
        public const int Unknown = 0,
            Enter = 10,
            Exit = 11,
            Exception = 12,
            Request = 20,
            RequestOk = 21,
            RequestTimeout = 22,
            OperationSucceeded = 100;
    }

    internal static class Logging
    {
        private static readonly TraceSource source = new TraceSource("Magik");

        public static void Enter(object obj, object param = null, [CallerMemberName] string memberName = null)
        {
            //source.Switch = source.Switch;
            source.TraceEvent(TraceEventType.Verbose, EventId.Enter,
                $"{ToString(obj)}.{memberName} <| {param}");
        }

        public static void Exit(object obj, string result = null, [CallerMemberName] string memberName = null)
        {
            source.TraceEvent(TraceEventType.Verbose, EventId.Exit,
                $"{ToString(obj)}.{memberName} -> {result}");
        }

        //public static T Exit<T>(object obj, T result, [CallerMemberName] string memberName = null)
        //{
        //    Exit(obj, Convert.ToString(result), memberName);
        //    return result;
        //}

        /// <summary>
        /// 向日志输出一条诊断信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public static void Trace(object obj, int id, string format, params object[] args)
        {
            source.TraceEvent(TraceEventType.Verbose, id,
                $"{ToString(obj)} : {string.Format(format, args)}");
        }

        /// <summary>
        /// 向日志输出一条诊断信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public static void Trace(object obj, string format, params object[] args)
        {
            Trace(obj, 0, format, args);
        }

        /// <summary>
        /// 向日志输出一条警告信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public static void Warn(object obj, string format, params object[] args)
        {
            Warn(obj, 0, format, args);
        }

        /// <summary>
        /// 向日志输出一条警告信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public static void Warn(object obj, int id, string format, params object[] args)
        {
            source.TraceEvent(TraceEventType.Warning, id,
                $"{ToString(obj)} : {string.Format(format, args)}");
        }

        /// <summary>
        /// 向日志输出一条信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public static void Info(object obj, int id, string format, params object[] args)
        {
            source.TraceEvent(TraceEventType.Information, id,
                $"{ToString(obj)} : {string.Format(format, args)}");
        }

        /// <summary>
        /// 向日志输出一条信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public static void Info(object obj, string format, params object[] args)
        {
            Info(obj, 0, format, args);
        }

        /// <summary>
        /// 向日志输出一条表示成功的信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public static void Success(object obj, string format, params object[] args)
        {
            Info(obj, EventId.OperationSucceeded, format, args);
        }

        /// <summary>
        /// 向日志输出一条错误信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public static void Error(object obj, string format, params object[] args)
        {
            source.TraceEvent(TraceEventType.Error, 0,
                $"{ToString(obj)} : {string.Format(format, args)}");
        }

        /// <summary>
        /// 向日志输出一条异常信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="ex">要输出的异常信息。</param>
        public static void Exception(object obj, Exception ex, [CallerMemberName] string memberName = null)
        {
            source.TraceEvent(TraceEventType.Error, EventId.Exception,
                $"{ToString(obj)}.{memberName} !> {ex}");
        }

        private static string ToString(object obj)
        {
            if (obj == null) return "-";
            string content;
            if (obj is KgNode)
                content = Convert.ToString(((KgNode) obj).Id);
            else
                content = Convert.ToString(obj.GetHashCode());
            return $"{obj.GetType().Name}#{content}";
        }
    }
}

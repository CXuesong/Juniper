using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Contests.Bop.Participants.Magik.Analysis;

namespace Microsoft.Contests.Bop.Participants.Magik
{
    internal class EventId
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

    /// <summary>
    /// 负责日志的编写。
    /// </summary>
    internal class Logger
    {
        public static readonly Logger Magik = new Logger("Magik");
        public static readonly Logger AcademicSearch = new Logger("Magik.AcademicSearch");

        private readonly TraceSource source;

        public Logger(string name)
        {
            source = new TraceSource(name);
        }

        [Conditional("TRACE_MORE")]
        public void Enter(object obj, object param = null, [CallerMemberName] string memberName = null)
        {
            if (source.Switch.ShouldTrace(TraceEventType.Verbose))
                source.TraceEvent(TraceEventType.Verbose, EventId.Enter,
                $"{ToString(obj)}.{memberName} <| {param}");
        }

        [Conditional("TRACE_MORE")]
        public void Exit(object obj, string result = null, [CallerMemberName] string memberName = null)
        {
            if (source.Switch.ShouldTrace(TraceEventType.Verbose))
                source.TraceEvent(TraceEventType.Verbose, EventId.Exit,
                $"{ToString(obj)}.{memberName} -> {result}");
        }

        //public  T Exit<T>(object obj, T result, [CallerMemberName] string memberName = null)
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
        public void Trace(object obj, int id, string format, params object[] args)
        {
            if (source.Switch.ShouldTrace(TraceEventType.Verbose))
                source.TraceEvent(TraceEventType.Verbose, id,
                    $"{ToString(obj)} : {string.Format(format, args)}");
        }

        /// <summary>
        /// 向日志输出一条诊断信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public void Trace(object obj, string format, params object[] args)
        {
            Trace(obj, 0, format, args);
        }

        /// <summary>
        /// 向日志输出一条警告信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public void Warn(object obj, string format, params object[] args)
        {
            Warn(obj, 0, format, args);
        }

        /// <summary>
        /// 向日志输出一条警告信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public void Warn(object obj, int id, string format, params object[] args)
        {
            if (source.Switch.ShouldTrace(TraceEventType.Warning))
                source.TraceEvent(TraceEventType.Warning, id,
                $"{ToString(obj)} : {string.Format(format, args)}");
        }

        /// <summary>
        /// 向日志输出一条信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public void Info(object obj, int id, string format, params object[] args)
        {
            if (source.Switch.ShouldTrace(TraceEventType.Information))
                source.TraceEvent(TraceEventType.Information, id,
                $"{ToString(obj)} : {string.Format(format, args)}");
        }

        /// <summary>
        /// 向日志输出一条信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public void Info(object obj, string format, params object[] args)
        {
            Info(obj, 0, format, args);
        }

        /// <summary>
        /// 向日志输出一条表示成功的信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public void Success(object obj, string format, params object[] args)
        {
            Info(obj, EventId.OperationSucceeded, format, args);
        }

        /// <summary>
        /// 向日志输出一条错误信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="format">诊断信息的格式化字符串。</param>
        /// <param name="args">格式化字符串的参数。</param>
        public void Error(object obj, string format, params object[] args)
        {
            if (source.Switch.ShouldTrace(TraceEventType.Error))
                source.TraceEvent(TraceEventType.Error, 0,
                $"{ToString(obj)} : {string.Format(format, args)}");
        }

        /// <summary>
        /// 向日志输出一条异常信息。
        /// </summary>
        /// <param name="obj">发出诊断信息的源对象。</param>
        /// <param name="ex">要输出的异常信息。</param>
        public void Exception(object obj, Exception ex, [CallerMemberName] string memberName = null)
        {
            if (source.Switch.ShouldTrace(TraceEventType.Error))
                source.TraceEvent(TraceEventType.Error, EventId.Exception,
                $"{ToString(obj)}.{memberName} !> {Utility.ExpandErrorMessage(ex)}");
        }

        private string ToString(object obj)
        {
            if (obj == null) return "-";
            string content = obj as string;
            if (content != null) return content;
            if (obj is KgNode)
                content = Convert.ToString(((KgNode)obj).Id);
            else
                content = Convert.ToString(obj.GetHashCode());
            return obj.GetType().Name + "#" + content;
        }
    }

    /// <summary>
    /// 用于发布与计时相关的日志。
    /// </summary>
    public static class TimerLogger
    {
        /// <summary>
        /// 事件跟踪源。其名称为 Magik.Timers 。
        /// </summary>
        public static TraceSource TraceSource { get; } = new TraceSource("Magik.Timers");

        /// <summary>
        /// 将一条简明的计时消息写入日志。
        /// </summary>
        public static void TraceTimer(string name, Stopwatch sw)
        {
            if (TraceSource.Switch.ShouldTrace(TraceEventType.Information))
                TraceSource.TraceInformation(DateTime.Now.ToString("O") + "\t" + name + "\t" + sw.ElapsedMilliseconds);
        }
    }
}

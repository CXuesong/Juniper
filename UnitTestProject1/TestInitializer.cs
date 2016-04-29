using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTestProject1
{
    [TestClass]
    public class TestInitializer
    {
        //private static readonly TraceSource magikSource = new TraceSource("Magik");
        //private static readonly TraceSource magikAcademicSource = new TraceSource("Magik.Academic");
        //private static TextWriterTraceListener listener = new TextWriterTraceListener("D:\\123.txt") { Name = "ttt" };
        private static TestContext thisTestContext;

        public class TestContextTraceListener : TraceListener
        {
            public TestContextTraceListener()
            {
            }

            private string buffer;

            /// <summary>
            /// 在派生类中被重写时，向在该派生类中所创建的侦听器写入指定消息。
            /// </summary>
            /// <param name="message">要写入的消息。</param>
            public override void Write(string message)
            {
                buffer += message;
            }

            /// <summary>
            /// 在派生类中被重写时，向在该派生类中所创建的侦听器写入消息，后跟行结束符。
            /// </summary>
            /// <param name="message">要写入的消息。</param>
            public override void WriteLine(string message)
            {
                thisTestContext?.WriteLine(buffer + message);
                buffer = null;
            }

            /// <summary>
            /// 在派生类中被重写时，刷新输出缓冲区。
            /// </summary>
            public override void Flush()
            {
                thisTestContext?.WriteLine(buffer);
                buffer = null;
                base.Flush();
            }
        }

        [AssemblyInitialize]
        public static void OnAssemblyInitialize(TestContext context)
        {
            thisTestContext = context;
        }

        [AssemblyCleanup]
        public static void OnAssemblyCleanup()
        {

        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace UnitTestProject1
{
    static class TestUtility
    {
        /// <summary>
        /// 同步执行 Task ，并返回其结果。
        /// </summary>
        public static T Await<T>(Task<T> task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            try
            {
                task.Wait();
                return task.Result;
            }
            catch (AggregateException ex)
            {
                // 展开内部异常。
                if (ex.InnerExceptions.Count == 1)
                    ExceptionDispatchInfo.Capture(ex.InnerExceptions[0]).Throw();
                throw;
            }
       }
    }
}

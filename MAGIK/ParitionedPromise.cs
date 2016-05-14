using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Contests.Bop.Participants.Magik
{
    /// <summary>
    /// 表示一个分阶段完成的 Promise 。
    /// </summary>
    /// <remarks>
    /// 请务必调用 <see cref="WhenCompleted"/> 函数。
    /// </remarks>
    public class ParitionedPromise<T>
    {
        private readonly List<Func<T, Task>> _WhenPartitionFinished = new List<Func<T, Task>>(1);
        private readonly List<Task> _AssignedTasks = new List<Task>();
        private List<T> buffer = new List<T>();
        private Task _ProducerTask;
        private Task _WhenCompletedTask;

        /// <summary>
        /// 确定在一个分区结束后，需要执行的任务。
        /// </summary>
        public ParitionedPromise<T> PartitionContinueWith(Func<T, Task> continuation)
        {
            if (continuation == null) throw new ArgumentNullException(nameof(continuation));
            if (buffer == null) throw new InvalidOperationException();
            if (buffer.Count > 0)
                _AssignedTasks.AddRange(buffer.SelectMany(pr =>
                    _WhenPartitionFinished.Select(f => f(pr))));
            _WhenPartitionFinished.Add(continuation);
            return this;
        }

        /// <summary>
        /// 生产方：表明已经产生一个结果。
        /// </summary>
        public void DeclarePartitionFinished(T partitionResult)
        {
            // 考虑当调用方还没来得及 PartitionContinueWith 时，
            // 生产方就产生了一个或多个结果。
            buffer?.Add(partitionResult);
            _AssignedTasks.AddRange(_WhenPartitionFinished.Select(f => f(partitionResult)));
        }

        /// <summary>
        /// 返回一个 <see cref="Task"/>，在当前任务与 PartitionFinished 全部执行完毕后结束。
        /// </summary>
        /// <returns>
        /// 调用此函数后，将不能使用 <see cref="PartitionContinueWith" />
        /// </returns>
        public Task WhenCompleted()
        {
            buffer = null;
            if (_ProducerTask == null || _WhenCompletedTask != null)
                throw new InvalidOperationException();
            _WhenCompletedTask = _ProducerTask.ContinueWith((t, at) =>
                Task.WhenAll((IList<Task>) at), _AssignedTasks);
            return _WhenCompletedTask;
        }

        /// <summary>
        /// 设置生产者 Task 。在此任务和所有的 PartitionContinueWith 任务结束后，
        /// WhenCompleted 任务会结束。
        /// 此函数必须被生产者调用，且仅能调用一次。
        /// </summary>
        public void SetProducerTask(Task task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            if (_ProducerTask != null) throw new InvalidOperationException();
            _ProducerTask = task;
        }
    }
}

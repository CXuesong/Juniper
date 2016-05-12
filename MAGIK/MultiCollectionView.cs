using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Contests.Bop.Participants.Magik
{
    /// <summary>
    /// 为 <see cref="MultiCollectionView{T}"/> 提供辅助功能。
    /// </summary>
    public static class MultiCollectionView
    {
        public static MultiCollectionView<T> Create<T>(params IReadOnlyCollection<T>[] collections)
        {
            return new MultiCollectionView<T>(collections);
        }
    }

    /// <summary>
    /// 由多个 <see cref="ICollection{T}"/> 组成的复合集合视图。
    /// </summary>
    public class MultiCollectionView<T> : IReadOnlyCollection<T>
    {
        private readonly IReadOnlyCollection<T>[] collections;

        public MultiCollectionView(IReadOnlyCollection<T>[] collections)
        {
            if (collections == null) throw new ArgumentNullException(nameof(collections));
            this.collections = collections;
        }

        /// <summary>
        /// 返回一个循环访问集合的枚举器。
        /// </summary>
        /// <returns>
        /// 用于循环访问集合的枚举数。
        /// </returns>
        public IEnumerator<T> GetEnumerator()
        {
            return collections.SelectMany(c => c).GetEnumerator();
        }

        /// <summary>
        /// 返回循环访问集合的枚举数。
        /// </summary>
        /// <returns>
        /// 可用于循环访问集合的 <see cref="T:System.Collections.IEnumerator"/> 对象。
        /// </returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// 获取集合中的元素数。
        /// </summary>
        /// <returns>
        /// 集合中的元素数。
        /// </returns>
        public int Count => collections.Sum(c => c.Count);
    }
}

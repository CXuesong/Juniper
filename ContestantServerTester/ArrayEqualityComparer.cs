using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContestantServerTester
{
    /// <summary>
    /// 用于比较两个 <see cref="T"/> 数组的元素相等性。
    /// </summary>
    public class ArrayEqualityComparer<T> : EqualityComparer<T[]>
    {
        public new static readonly ArrayEqualityComparer<T> Default = new ArrayEqualityComparer<T>();

        public ArrayEqualityComparer()
        {
            ItemComparer = EqualityComparer<T>.Default;
        }

        public ArrayEqualityComparer(IEqualityComparer<T> itemComparer)
        {
            ItemComparer = itemComparer;
        }

        public IEqualityComparer<T> ItemComparer { get; }

        /// <summary>
        /// 在派生类中重写时，确定类型两个对象是否相等。
        /// </summary>
        /// <returns>
        /// 如果指定的对象相等，则为 true；否则为 false。
        /// </returns>
        /// <param name="x">要比较的第一个对象。</param><param name="y">要比较的第二个对象。</param>
        public override bool Equals(T[] x, T[] y)
        {
            if (x == null) return y == null;
            if (x.Length != y?.Length) return false;
            return x.SequenceEqual(y, ItemComparer);
        }

        /// <summary>
        /// 在派生类中重写时，用作指定对象的哈希算法和数据结构（如哈希表）的哈希函数。
        /// </summary>
        /// <returns>
        /// 指定对象的哈希代码。
        /// </returns>
        /// <param name="obj">要为其获取哈希代码的对象。</param><exception cref="T:System.ArgumentNullException">The type of <paramref name="obj"/> is a reference type and <paramref name="obj"/> is null.</exception>
        public override int GetHashCode(T[] obj)
        {
            if (obj == null || obj.Length == 0) return 0;
            var len = obj.Length;
            int hash = len.GetHashCode();
            for (int i = 0, j = Math.Min(16, len); i < j; i++)
                hash = unchecked(hash * 13 + ItemComparer.GetHashCode(obj[i]));
            for (int i = 17; i < len; i += 13)
                hash = unchecked(hash * 17 + ItemComparer.GetHashCode(obj[i]));
            return hash;
        }
    }
}

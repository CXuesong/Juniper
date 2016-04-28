using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Contests.Bop.Participants.Magik.Analysis
{
    /// <summary>
    /// 表示知识图节点的集合。
    /// </summary>
    public class KgNodeCollection : KeyedCollection<long, KgNode>
    {
        /// <summary>
        /// 在派生类中实现时，将从指定元素提取键。
        /// </summary>
        /// <returns>
        /// 指定元素的键。
        /// </returns>
        /// <param name="item">从中提取键的元素。</param>
        protected override long GetKeyForItem(KgNode item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            return item.Id;
        }
    }
}

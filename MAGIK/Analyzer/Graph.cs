using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Contests.Bop.Participants.Magik.Analyzer
{
    /// <summary>
    /// 表示一个泛型的无向图。
    /// </summary>
    /// <remarks>
    /// 无向图的节点可以包含任何信息，由 <typeparamref name="TVertex"/> 指定其类型。
    /// 此类型使用邻接表来存储节点之间的连接关系。连接关系是简单关系，不能包含更多信息（如权重）。
    /// </remarks>
    public class Graph<TVertex>
    {
        // Vertex, Adjacent Vertices
        private readonly Dictionary<TVertex, ICollection<TVertex>> vertices = new Dictionary<TVertex, ICollection<TVertex>>();
        private int _EdgesCount = 0;

        //private static readonly ICollection<TVertex> EmptyVertices = new TVertex[0];

        /// <summary>
        /// 添加一个节点。
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="vertex"/> 为 <c>null</c> 。</exception>
        /// <exception cref="ArgumentException">当前图中已经存在指定的节点。</exception>
        public void Add(TVertex vertex)
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));
            vertices.Add(vertex, new List<TVertex>());
        }

        /// <summary>
        /// 添加一条边。
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="vertex1"/> 或 <paramref name="vertex2"/> 为 <c>null</c> 。</exception>
        /// <exception cref="KeyNotFoundException">找不到指定的端点节点。</exception>
        /// <exception cref="ArgumentException">试图添加一个自环。</exception>
        public void Add(TVertex vertex1, TVertex vertex2)
        {
            if (vertex1 == null) throw new ArgumentNullException(nameof(vertex1));
            if (vertex2 == null) throw new ArgumentNullException(nameof(vertex2));
            if (vertices.Comparer.Equals(vertex1, vertex2))
                throw new ArgumentException("不允许自环。");
            // 虽然理论上是可以处理重边的情况，但在此应用中不应当出现重边。
            // 此处假定不出现重边。
            Debug.Assert(!vertices[vertex1].Contains(vertex2));
            Debug.Assert(!vertices[vertex2].Contains(vertex1));
            vertices[vertex1].Add(vertex2);
            vertices[vertex2].Add(vertex1);
            _EdgesCount++;
        }

        /// <summary>
        /// 获取指定节点的邻节点。
        /// </summary>
        /// <exception cref="KeyNotFoundException">在当前图中找不到指定的节点。</exception>
        public ICollection<TVertex> AdjacentVertices(TVertex vertex)
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));
            return vertices[vertex];
        }

        /// <summary>
        /// 获取节点的数量。
        /// </summary>
        public int VerticesCount => vertices.Count;

        /// <summary>
        /// 获取边的数量。
        /// </summary>
        public int EdgesCount => _EdgesCount;
    }
}

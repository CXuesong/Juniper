using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Microsoft.Contests.Bop.Participants.Magik.Analysis
{
    /// <summary>
    /// 表示一个泛型的有向图。
    /// </summary>
    /// <remarks>
    /// 有向图的节点可以包含任何信息，由 <typeparamref name="TVertex"/> 指定其类型。
    /// 此类型使用邻接表来存储节点之间的连接关系。连接关系是简单关系，不能包含更多信息（如权重）。
    /// 此有向图不支持自环、不支持方向相同的重边。
    /// </remarks>
    public class DirectedGraph<TVertex>
    {
        // Vertex, Adjacent Vertices
        private readonly Dictionary<TVertex, VertexEntry> vertices = new Dictionary<TVertex, VertexEntry>();
        private int _EdgesCount = 0;

        //private static readonly ICollection<TVertex> EmptyVertices = new TVertex[0];

        /// <summary>
        /// 添加一个节点。
        /// </summary>
        /// <returns>如果成功添加节点，则返回<c>true</c>。否则，如果节点已经存在，则返回<c>false</c>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="vertex"/> 为 <c>null</c> 。</exception>
        public bool Add(TVertex vertex)
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));
            if (vertices.ContainsKey(vertex)) return false;
            vertices.Add(vertex, new VertexEntry(vertex));
            return true;
        }

        /// <summary>
        /// 添加一条边。如果端点不存在，则将断点加入图。
        /// </summary>
        /// <returns>如果成功添加边，则返回<c>true</c>。否则，如果相同端点、相同方向的边已经存在，则返回<c>false</c>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="vertex1"/> 或 <paramref name="vertex2"/> 为 <c>null</c> 。</exception>
        /// <exception cref="ArgumentException">试图添加一个自环。</exception>
        public bool Add(TVertex vertex1, TVertex vertex2)
        {
            return Add(vertex1, vertex2, false);
        }

        /// <summary>
        /// 添加一条有向边。
        /// </summary>
        /// <param name="vertex1">源点。（Source）</param>
        /// <param name="vertex2">漏点。（Sink）</param>
        /// <param name="noVerticesCreation">不允许在添加边的同时创建节点。</param>
        /// <returns>如果成功添加边，则返回<c>true</c>。否则，如果边已经存在，则返回<c>false</c>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="vertex1"/> 或 <paramref name="vertex2"/> 为 <c>null</c> 。</exception>
        /// <exception cref="KeyNotFoundException">找不到指定的端点节点。</exception>
        /// <exception cref="ArgumentException">试图添加一个自环。</exception>
        public bool Add(TVertex vertex1, TVertex vertex2, bool noVerticesCreation)
        {
            if (vertex1 == null) throw new ArgumentNullException(nameof(vertex1));
            if (vertex2 == null) throw new ArgumentNullException(nameof(vertex2));
            if (vertices.Comparer.Equals(vertex1, vertex2))
                throw new ArgumentException("不允许自环。");
            // 在此应用中不应当出现重边。
            var ve1 = GetVertexEntry(vertex1, !noVerticesCreation);
            var ve2 = GetVertexEntry(vertex2, !noVerticesCreation);
            if (!ve1.AdjacentOutVertices.Add(vertex2))
                return false;
            ve2.AdjacentInVertices.Add(vertex1);
            _EdgesCount++;
            return true;
        }

        /// <summary>
        /// 获取图中的所有节点。
        /// </summary>
        public ICollection<TVertex> Vertices => vertices.Keys;

        /// <summary>
        /// 获取指定节点的、指向指定点的邻节点。
        /// </summary>
        /// <exception cref="KeyNotFoundException">在当前图中找不到指定的节点。</exception>
        public IReadOnlyCollection<TVertex> AdjacentInVertices(TVertex vertex)
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));
            return vertices[vertex].ReadonlyAdjacentInVertices;
        }

        /// <summary>
        /// 获取指定节点的、由指定点指向的邻节点。
        /// </summary>
        /// <exception cref="KeyNotFoundException">在当前图中找不到指定的节点。</exception>
        public IReadOnlyCollection<TVertex> AdjacentOutVertices(TVertex vertex)
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));
            return vertices[vertex].ReadonlyAdjacentOutVertices;
        }

        /// <summary>
        /// 获取节点的数量。
        /// </summary>
        public int VerticesCount => vertices.Count;

        /// <summary>
        /// 获取边的数量。
        /// </summary>
        public int EdgesCount => _EdgesCount;

        private VertexEntry GetVertexEntry(TVertex vertex, bool allowCreation)
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));
            VertexEntry e;
            // Get or Create
            if (!vertices.TryGetValue(vertex, out e))
            {
                if (!allowCreation) throw new KeyNotFoundException();
                e = new VertexEntry(vertex);
                vertices.Add(vertex, e);
            }
            return e;
        }

        private class VertexEntry
        {
            public HashSet<TVertex> AdjacentInVertices { get; }

            public HashSet<TVertex> AdjacentOutVertices { get; }


            #region 用于向外部公开的“只读”集合

            public IReadOnlyCollection<TVertex> ReadonlyAdjacentInVertices { get; }

            public IReadOnlyCollection<TVertex> ReadonlyAdjacentOutVertices { get; }

            #endregion

            /// <param name="vertex">此 VertexEntry 对应的节点。</param>
            public VertexEntry(TVertex vertex)
            {
                // 目前没有在 VertexEntry 中保存 vertex 的必要性。
                // 只是为结构体选择一个不同的重载而已。
                // TODO 获取 HashSet 的只读包装，即真正的 只读 集合。
                AdjacentInVertices = new HashSet<TVertex>();
                AdjacentOutVertices = new HashSet<TVertex>();
                ReadonlyAdjacentInVertices = AdjacentInVertices;
                ReadonlyAdjacentOutVertices = AdjacentOutVertices;
            }
        }
    }
}

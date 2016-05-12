using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.Contests.Bop.Participants.Magik.Analysis
{
    /// <summary>
    /// 表示一个线程安全的泛型有向图。
    /// </summary>
    /// <remarks>
    /// 有向图的节点可以包含任何信息，由 <typeparamref name="TVertex"/> 指定其类型。
    /// 此类型使用邻接表来存储节点之间的连接关系。连接关系是简单关系，不能包含更多信息（如权重）。
    /// 此有向图不支持自环、不支持方向相同的重边。
    /// </remarks>
    public class DirectedGraph<TVertex> : IDisposable
    {
        // Vertex, Adjacent Vertices
        private ConcurrentDictionary<TVertex, VertexEntry> vertices = new ConcurrentDictionary<TVertex, VertexEntry>();
        private int _EdgesCount = 0;

        //private static readonly ICollection<TVertex> EmptyVertices = new TVertex[0];

        /// <summary>
        /// 添加一个节点。
        /// </summary>
        /// <returns>如果成功添加节点，则返回<c>true</c>。否则，如果节点已经存在，则返回<c>false</c>。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="vertex"/> 为 <c>null</c> 。</exception>
        public void Add(TVertex vertex)
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));
            vertices.GetOrAdd(vertex, k => new VertexEntry(k));
        }

        /// <summary>
        /// 添加一条有向边。如果端点不存在，则将端点加入图。
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
            AssertNotDisposed();
            if (EqualityComparer<TVertex>.Default.Equals(vertex1, vertex2))
                throw new ArgumentException("不允许自环。");
            // 在此应用中不应当出现重边。
            var ve1 = GetVertexEntry(vertex1, !noVerticesCreation);
            var ve2 = GetVertexEntry(vertex2, !noVerticesCreation);
            ve1.SyncLock.EnterWriteLock();
            try
            {
                if (!ve1.AdjacentOutVertices.Add(vertex2))
                    return false;
            }
            finally
            {
                ve1.SyncLock.ExitWriteLock();
            }
            ve2.SyncLock.EnterWriteLock();
            try
            {
                ve2.AdjacentInVertices.Add(vertex1);
            }
            finally
            {
                ve2.SyncLock.ExitWriteLock();
            }
            Interlocked.Increment(ref _EdgesCount);
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
        public ISet<TVertex> AdjacentInVertices(TVertex vertex)
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));
            AssertNotDisposed();
            return vertices[vertex].DuplicateAdjacentInVertices();
        }

        /// <summary>
        /// 获取指定节点的、由指定点指向的邻节点。
        /// </summary>
        /// <exception cref="KeyNotFoundException">在当前图中找不到指定的节点。</exception>
        public ISet<TVertex> AdjacentOutVertices(TVertex vertex)
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));
            AssertNotDisposed();
            return vertices[vertex].DuplicateAdjacentOutVertices();
        }

        /// <summary>
        /// 获取节点的数量。
        /// </summary>
        public int VerticesCount => vertices.Count;

        /// <summary>
        /// 获取边的数量。
        /// </summary>
        public int EdgesCount => _EdgesCount;

        /// <summary>
        /// 判断图中是否包含某节点。
        /// </summary>
        public bool Contains(TVertex vertex)
        {
            AssertNotDisposed();
            return vertices.ContainsKey(vertex);
        }

        /// <summary>
        /// 判断图中是否包含某条有向边。
        /// </summary>
        public bool Contains(TVertex vertex1, TVertex vertex2)
        {
            AssertNotDisposed();
            var ve1 = vertices.TryGetValue(vertex1);
            if (ve1 == null) return false;
            ve1.SyncLock.EnterReadLock();
            try
            {
                return ve1.AdjacentOutVertices.Contains(vertex2);
            }
            finally
            {
                ve1.SyncLock.ExitReadLock();
            }
        }

        private VertexEntry GetVertexEntry(TVertex vertex, bool allowCreation)
        {
            if (vertex == null) throw new ArgumentNullException(nameof(vertex));
            Debug.Assert(!disposedValue);
            // Get or Create
            if (allowCreation)
                return vertices.GetOrAdd(vertex, k => new VertexEntry(k));
            else
                return vertices[vertex];
        }

        /// <summary>
        /// 获取有向图中所有边的字符串表示形式。注意，对于大规模的网络，此操作可能会非常慢。
        /// </summary>
        public string Dump()
        {
            AssertNotDisposed();
            return string.Join("\n", vertices.SelectMany(p =>
                p.Value.AdjacentOutVertices.Select(v2 => $"{p.Key}, {v2}")));
        }

        /// <summary>
        /// 用于在字典中保存节点的附加信息，如邻接表。
        /// </summary>
        private class VertexEntry : IDisposable
        {
            public HashSet<TVertex> AdjacentInVertices { get; private set; }

            public HashSet<TVertex> AdjacentOutVertices { get; private set; }

            public ReaderWriterLockSlim SyncLock { get; } = new ReaderWriterLockSlim();

            #region 用于向外部公开的集合复制逻辑

            public ISet<TVertex> DuplicateAdjacentInVertices()
            {
                Debug.Assert(!disposedValue);
                SyncLock.EnterReadLock();
                var set = AdjacentInVertices.Clone();
                SyncLock.ExitReadLock();
                return set;
            }
            public ISet<TVertex> DuplicateAdjacentOutVertices()
            {
                Debug.Assert(!disposedValue);
                SyncLock.EnterReadLock();
                var set = AdjacentOutVertices.Clone();
                SyncLock.ExitReadLock();
                return set;
            }

            #endregion

            /// <param name="vertex">此 VertexEntry 对应的节点。</param>
            public VertexEntry(TVertex vertex)
            {
                // 目前没有在 VertexEntry 中保存 vertex 的必要性。
                AdjacentInVertices = new HashSet<TVertex>();
                AdjacentOutVertices = new HashSet<TVertex>();
            }

            #region IDisposable Support
            private bool disposedValue = false; // 要检测冗余调用

            protected virtual void Dispose(bool disposing)
            {
                if (disposedValue) return;
                disposedValue = true;
                if (disposing)
                {
                    // 释放托管状态(托管对象)。
                    SyncLock.Dispose();
                }
                // 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // 将大型字段设置为 null。
                AdjacentInVertices = null;
                AdjacentOutVertices = null;
            }

            //// 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
            // ~VertexEntry()
            //{
            //    // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            //    Dispose(false);
            //}

            // 添加此代码以正确实现可处置模式。
            public void Dispose()
            {
                // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
                Dispose(true);
                //// 如果在以上内容中替代了终结器，则取消注释以下行。
                //GC.SuppressFinalize(this);
            }
            #endregion

        }

        #region IDisposable Support
        private bool disposedValue; // 要检测冗余调用

        private void AssertNotDisposed()
        {
            if (vertices == null) throw new ObjectDisposedException(ToString());
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;
                if (disposing)
                {
                    // 释放托管状态(托管对象)。
                    foreach (var v in vertices.Values)
                        v.Dispose();
                }
                // 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // 将大型字段设置为 null。
                //vertices = null;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        // ~DirectedGraph() {
        //   // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
        //   Dispose(false);
        // }

        // 添加此代码以正确实现可处置模式。
        public void Dispose()
        {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // 如果在以上内容中替代了终结器，则取消注释以下行。
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}

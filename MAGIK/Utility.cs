using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Contests.Bop.Participants.Magik
{
    public static partial class Utility
    {
        public static string ToTitleCase(this string str)
        {
            if (str == null) throw new ArgumentNullException(nameof(str));
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(str);
        }

        public static string Truncate(this string str, int length)
        {
            if (str == null) throw new ArgumentNullException(nameof(str));
            if (str.Length <= length) return str;
            if (length > 6) return str.Substring(0, length - 3) + "...";
            return str.Substring(0, 3);
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new HashSet<T>(source);
        }

        public static Exception BuildIdNotFoundException(long id)
        {
            return new KeyNotFoundException($"在 MAG 中找不到指定的 Id：{id}");
        }

        public static TValue GetOrCreate<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
            where TValue : new()
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));
            TValue val;
            if (!dict.TryGetValue(key, out val))
            {
                val = new TValue();
                dict.Add(key, val);
            }
            return val;
        }

        public static TValue TryGetValue<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key)
        {
            if (dict == null) throw new ArgumentNullException(nameof(dict));
            TValue val;
            if (dict.TryGetValue(key, out val)) return val;
            return default(TValue);
        }

        public static KeyValuePair<TKey, TValue> CreateKeyValuePair<TKey, TValue>(TKey key, TValue value)
        {
            return new KeyValuePair<TKey, TValue>(key, value);
        }

        /// <summary>
        /// 将一个 <see cref="IEnumerable{T}"/> 按照固定的数量分组。
        /// </summary>
        public static IEnumerable<IEnumerable<T>> Partition<T>(this IEnumerable<T> source, int partitionSize)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (partitionSize <= 0) throw new ArgumentOutOfRangeException(nameof(partitionSize));
            var partition = new List<T>(partitionSize);
            foreach (var item in source)
            {
                partition.Add(item);
                if (partition.Count == partitionSize)
                {
                    yield return partition;
                    partition.Clear();
                }
            }
            if (partition.Count > 0) yield return partition;
        }
    }

    /// <summary>
    /// 表示一个键-值对。在进行相等性比较时，使用键的 HashCode 和相等性进行比较。
    /// </summary>
    public struct KeyValuePair<TKey, TValue> : IEquatable<KeyValuePair<TKey, TValue>>
    {
        public KeyValuePair(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }

        /// <summary>
        /// 在使用 <see cref="Equals" /> 和 <see cref="GetHashCode" /> 进行比较时所采用键。
        /// </summary>
        public TKey Key { get; }

        public TValue Value { get; }

        /// <summary>
        /// 指示当前对象是否等于同一类型的另一个对象。
        /// </summary>
        /// <returns>
        /// 如果当前对象等于 <paramref name="other"/> 参数，则为 true；否则为 false。
        /// </returns>
        /// <param name="other">与此对象进行比较的对象。</param>
        public bool Equals(KeyValuePair<TKey, TValue> other)
        {
            return EqualityComparer<TKey>.Default.Equals(Key, other.Key);
        }

        /// <summary>
        /// 返回此实例的哈希代码。
        /// </summary>
        /// <returns>
        /// 一个 32 位有符号整数，它是该实例的哈希代码。
        /// </returns>
        public override int GetHashCode()
        {
            return Key?.GetHashCode() ?? 0;
        }
    }

    public class NamedObject
    {
        public string Name { get; }

        public NamedObject(string name)
        {
            Name = name;
        }

        /// <summary>
        /// 返回表示当前对象的字符串。
        /// </summary>
        /// <returns>
        /// 表示当前对象的字符串。
        /// </returns>
        public override string ToString() => Name;
    }

    public static class HashSetExtensions
    {
        /// <summary>
        /// 复制一个 <see cref="HashSet{T}"/> 。
        /// </summary>
        /// <remarks>
        /// http://stackoverflow.com/questions/3927789/efficient-way-to-clone-a-hashsett
        /// </remarks>
        public static HashSet<T> Clone<T>(this HashSet<T> original)
        {
            if (original == null) throw new ArgumentNullException(nameof(original));
            var clone = (HashSet<T>) FormatterServices.GetUninitializedObject(typeof (HashSet<T>));
            Copy(Fields<T>.comparer, original, clone);
            if (original.Count == 0)
            {
                Fields<T>.freeList.SetValue(clone, -1);
            }
            else
            {
                Fields<T>.count.SetValue(clone, original.Count);
                Clone(Fields<T>.buckets, original, clone);
                Clone(Fields<T>.slots, original, clone);
                Copy(Fields<T>.freeList, original, clone);
                Copy(Fields<T>.lastIndex, original, clone);
                Copy(Fields<T>.version, original, clone);
            }

            return clone;
        }

        private static void Copy<T>(FieldInfo field, HashSet<T> source, HashSet<T> target)
        {
            field.SetValue(target, field.GetValue(source));
        }

        private static void Clone<T>(FieldInfo field, HashSet<T> source, HashSet<T> target)
        {
            field.SetValue(target, ((Array) field.GetValue(source)).Clone());
        }

        private static class Fields<T>
        {
            public static readonly FieldInfo freeList = GetFieldInfo("m_freeList");
            public static readonly FieldInfo buckets = GetFieldInfo("m_buckets");
            public static readonly FieldInfo slots = GetFieldInfo("m_slots");
            public static readonly FieldInfo count = GetFieldInfo("m_count");
            public static readonly FieldInfo lastIndex = GetFieldInfo("m_lastIndex");
            public static readonly FieldInfo version = GetFieldInfo("m_version");
            public static readonly FieldInfo comparer = GetFieldInfo("m_comparer");

            static FieldInfo GetFieldInfo(string name)
            {
                return typeof (HashSet<T>).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }
    }
}

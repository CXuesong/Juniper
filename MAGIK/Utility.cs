using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
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
    }
}

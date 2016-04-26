using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Contests.Bop.Participants.Magik
{
    public static partial class Utility
    {
        /// <summary>
        /// 适用于调试时使用的 Academic Search 搜索要求返回的属性列表。
        /// </summary>
        /// <remarks>
        /// 请参阅 https://www.microsoft.com/cognitive-services/en-us/academic-knowledge-api/documentation/entityattributes 。
        /// 另外，官网上把 J.JId 写成 J.Id ，表示我也是醉了。 CId 同理。
        /// </remarks>
        public const string DebugASEvaluationAttributes =
            "Id,Ti,Y,AA.AuN,AA.AuId,AA.AfN,AA.AfId,F.FN,F.FId,J.JN,J.JId,C.CN,C.CId,RId";

        private static string _AcademicSearchSubscriptionKey;

        /// <summary>
        /// 获取 Academic Search 订阅密钥。
        /// </summary>
        public static string AcademicSearchSubscriptionKey
        {
            get
            {
                if (_AcademicSearchSubscriptionKey == null)
                {
                    InitializeAcademicSearchSubscriptionKey();
                    Debug.Assert(_AcademicSearchSubscriptionKey != null,
                        " 在使用 MAGIK 项目前，需要在 _private/Utility.cs 中编写 InitializeAcademicSearchSubscriptionKey 函数的函数体，"
                        + "以将 _AcademicSearchSubscriptionKey 设置为您的 Academic Search 订阅密钥。"
                        + "\n请参阅 Utility.cs 以获取详情。");
                }
                return _AcademicSearchSubscriptionKey;
            }
        }

        /// <summary>
        /// 在使用 MAGIK 项目前，需要在 _private/Utility.cs 中编写此函数的函数体，
        /// 以将 _AcademicSearchSubscriptionKey 设置为您的 Academic Search
        /// 订阅密钥。
        /// </summary>
        /// <remarks>
        /// 例如
        /// <code>
        /// static partial class Utility
        /// {
        ///     static partial void InitializeAcademicSearchSubscriptionKey()
        ///     {
        ///         _AcademicSearchSubscriptionKey = ".......";
        ///     }
        /// }
        /// </code>
        /// </remarks>
        static partial void InitializeAcademicSearchSubscriptionKey();
        // 请在 _private/Utility.cs 路径下编写此函数的函数体，
        // 不要修改此处的函数头。
    }
}

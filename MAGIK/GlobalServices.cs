using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Contests.Bop.Participants.Magik.Academic;

namespace Microsoft.Contests.Bop.Participants.Magik
{
    /// <summary>
    /// 使用静态类公开一些全局性质的服务。
    /// </summary>
    public static partial class GlobalServices
    {
        /// <summary>
        /// 是否使用旗舰版的 Key 。
        /// </summary>
        /// <remarks>
        /// 请在 <see cref="InitializeConfidential" /> 的实现中设置此属性。
        /// </remarks>
        public static bool ASUseUltimateKey { get; private set; } = false;

        /// <summary>
        /// 在进行学术搜索时是否使用最小的请求属性集合，以期减少网络负载，提高性能。
        /// </summary>
        /// <remarks>
        /// 请在 <see cref="InitializeConfidential" /> 的实现中设置此属性。
        /// </remarks>
        public static bool ASReleaseMode { get; private set; }
#if DEBUG
            = false;
#else
            = true;
#endif

        /// <summary>
        /// 适用于 DEBUG 使用的 Academic Search 搜索要求返回的属性列表。
        /// </summary>
        /// <remarks>
        /// 请参阅 https://www.microsoft.com/cognitive-services/en-us/academic-knowledge-api/documentation/entityattributes 。
        /// 另外，官网上把 J.JId 写成 J.Id ，表示我也是醉了。 CId 同理。
        /// </remarks>
        public const string DebugASEvaluationAttributes =
            "Id,Ti,Y,AA.AuN,AA.AuId,AA.AfN,AA.AfId,F.FN,F.FId,J.JN,J.JId,C.CN,C.CId,RId";

        /// <summary>
        /// 适用于 RELEASE 使用的 Academic Search 搜索要求返回的属性列表。
        /// </summary>
        /// <remarks>
        /// 请参阅 https://www.microsoft.com/cognitive-services/en-us/academic-knowledge-api/documentation/entityattributes 。
        /// </remarks>
        public const string ReleaseASEvaluationAttributes =
            "Id,Y,AA.AuId,AA.AfId,F.FId,J.JId,C.CId,RId";

        /// <summary>
        /// 获取 Academic Search 订阅密钥。
        /// </summary>
        internal static string AcademicSearchSubscriptionKey { get; private set; }

        /// <summary>
        /// 创建一个新的 Academic Search 客户端。
        /// </summary>
        public static AcademicSearchClient CreateASClient()
        {
            var client = new AcademicSearchClient(AcademicSearchSubscriptionKey)
            {
                EvaluationDefaultAttributes = ASReleaseMode ? ReleaseASEvaluationAttributes: DebugASEvaluationAttributes,
                UserAgent = "MAGIK/1.0 (Windows)",
                Referer = "https://studentclub.msra.cn/bop2016/"
            };
            if (ASUseUltimateKey)
            {
                client.ServiceHostUrl = "https://oxfordhk.azure-api.net/academic/v1.0";
                client.QuerySuffix = "&subscription-key=" + AcademicSearchSubscriptionKey;
                //提醒：请使用以上访问方式，此key不可以使用学术搜索官网提供的访问方式。
            }
            Logger.Magik.Trace(null, $"AS 客户端已经创建： {client.ServiceHostUrl} 。");
            return client;
        }

        /// <summary>
        /// 在使用 MAGIK 项目前，需要在 _private/Confidential.cs 中编写此函数的函数体，
        /// 以将 AcademicSearchSubscriptionKey 设置为您的 Academic Search
        /// 订阅密钥。
        /// </summary>
        /// <remarks>
        /// 例如
        /// <code>
        /// static partial class GlobalServices
        /// {
        ///     static partial void InitializeConfidential()
        ///     {
        ///         if (ASUseUltimateKey)
        ///             AcademicSearchSubscriptionKey = "旗舰密钥";
        ///         else
        ///             AcademicSearchSubscriptionKey = "试用密钥";
        ///     }
        /// }
        /// </code>
        /// </remarks>
        static partial void InitializeConfidential();
        // 请在 _private/Utility.cs 路径下编写此函数的函数体，
        // 不要修改此处的函数头。

        static GlobalServices()
        {
            InitializeConfidential();
            Debug.Assert(AcademicSearchSubscriptionKey != null,
                "使用 MAGIK 项目前，需要在 _private/Confidential.cs 中编写 InitializeAcademicSearchSubscriptionKey 函数的函数体，"
                + "以将 AcademicSearchSubscriptionKey 设置为您的 Academic Search 订阅密钥。"
                + "\n请参阅 GlobalServices.cs 以获取详情。");
            Logger.Magik.Trace(null, "全局服务已经初始化完毕。");
        }
    }
}

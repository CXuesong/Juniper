using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Filters;
using Microsoft.Contests.Bop.Participants.Magik.MagikServer.Contract;
using Newtonsoft.Json;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikServer
{
    /// <summary>
    /// 为 HTTP 请求过程中发生的 <see cref="ArgumentException"/> 提供 JSON 回复生成方案。
    /// </summary>
    public class JsonArgumentExceptionFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            if (context.Exception is ArgumentException)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(
                        new
                        {
                            error = new Error(context.Exception.GetType().Name,
                                context.Exception.Message)
                        }), null, Utility.JsonMediaType)
                };
                context.Response = resp;
            }
        }
    }

    /// <summary>
    /// 为 HTTP 请求过程中发生的其它类型的异常提供状态为 500 的 JSON 回复生成方案。
    /// </summary>
    public class JsonExceptionsFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            if (context.Exception != null)
            {
                var resp = new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(
                        new
                        {
#if DEBUG
                            error = new Error(context.Exception.GetType().Name,
                                context.Exception.Message)
#else
                            error = new Error(context.Exception.GetType().Name,
                                "")
#endif
                        }), null, Utility.JsonMediaType)
                };
                context.Response = resp;
            }
        }
    }
}

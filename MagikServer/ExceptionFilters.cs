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
    /// 为 HTTP 请求过程中发生的 <see cref="ArgumentException"/> 提供回复生成方案。
    /// </summary>
    public class ArgumentExceptionFilterAttribute : ExceptionFilterAttribute
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
}

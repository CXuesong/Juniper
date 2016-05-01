using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Owin;
using Microsoft.Owin.Builder;
using Microsoft.Owin.Logging;
using Owin;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikServer
{
    public class Startup
    {
        ILogger requestLogger;

        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public void Configuration(IAppBuilder appBuilder)
        {
            // Configure Web API for self-host. 
            var startupLogger = appBuilder.CreateLogger("MagikServer.Startup");
            requestLogger = appBuilder.CreateLogger("MagikServer.Request");
            // 注册中间件。
            // The order in which you add middleware components
            // is generally the order in which they take effect
            // on the request, and then in reverse for the response. 
            var config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "{controller}/{id}",
                defaults: new {id = RouteParameter.Optional}
                );
            appBuilder.Use(RequestLoggingMiddleware);
            appBuilder.UseWebApi(config);
            startupLogger.WriteInformation("自承载 Web API 配置完毕。");
        }

        private async Task RequestLoggingMiddleware(IOwinContext context, Func<Task> next)
        {
            var request = context.Request;
            requestLogger.WriteVerbose($"{request.RemoteIpAddress} {request.Method}: {request.Uri}");
            await next();
            var response = context.Response;
            requestLogger.WriteVerbose($"{request.RemoteIpAddress} {request.Method}: {request.Uri} -> {response.StatusCode} {response.ContentType} {response.ContentLength}");
        }
    }
}

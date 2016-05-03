using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Owin;
using Microsoft.Owin.Builder;
using Microsoft.Owin.Extensions;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.Logging;
using Microsoft.Owin.StaticFiles;
using Owin;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikServer
{
    public class Startup
    {
        ILogger requestLogger;

        /// <summary>
        /// 用于保存网站内容的文件系统路径。
        /// </summary>
        public const string WwwFileSystemRoot = "wwwroot";
        /// <summary>
        /// 用于保存网站静态内容的虚拟路径。（目前尚未启用。）
        /// </summary>
        public const string StaticFilesVirtualRoot = "content";
        /// <summary>
        /// 用于保存网站静态内容的文件系统路径。（目前尚未启用。）
        /// </summary>
        public const string StaticFilesFileSystemRoot = WwwFileSystemRoot + "/content";

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
            appBuilder.Use(RequestLoggingMiddleware);
            {
                var config = new HttpConfiguration();
                config.MapHttpAttributeRoutes();
                config.Routes.IgnoreRoute("StaticFiles", StaticFilesVirtualRoot + "/{*pathInfo}");
                config.Routes.MapHttpRoute("Home", "", new {controller = "Home"});
                config.Routes.MapHttpRoute(
                    "DefaultApi",
                    "{controller}/{id}",
                    new { id = RouteParameter.Optional }
                    );
                appBuilder.UseWebApi(config);
            }
            /*
            {
                var options = new FileServerOptions
                {

                    FileSystem = new PhysicalFileSystem(StaticFilesFileSystemRoot),
                    EnableDefaultFiles = true,
                    EnableDirectoryBrowsing = false,
                    RequestPath = new PathString("/" + StaticFilesVirtualRoot)
                };
                appBuilder.UseFileServer(options);
            }
            */
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

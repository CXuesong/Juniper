using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Results;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikServer.Controllers
{
    public class HomeController : ApiController
    {

        public IHttpActionResult Get()
        {
            var sb = new StringBuilder(File.ReadAllText(Startup.WwwFileSystemRoot + @"/index.html"));
            using (var proc = Process.GetCurrentProcess())
            {
                sb.Replace("$WORKING_SET$", proc.WorkingSet64.ToString("#,#"));
                sb.Replace("$PEAK_WORKING_SET$", proc.PeakWorkingSet64.ToString("#,#"));
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(sb.ToString());
            response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/html");
            return new ResponseMessageResult(response);
        }
    }
}

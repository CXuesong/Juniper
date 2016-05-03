using System;
using System.Collections.Generic;
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
            var indexSource = new FileStream(Startup.WwwFileSystemRoot + @"/index.html", FileMode.Open, FileAccess.Read);
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StreamContent(indexSource);
            response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/html");
            return new ResponseMessageResult(response);
        }
    }
}

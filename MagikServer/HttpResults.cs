using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Newtonsoft.Json;

namespace Microsoft.Contests.Bop.Participants.Magik.MagikServer
{
    public class BadRequestResult : IHttpActionResult
    {
        public BadRequestResult(string content) : this(content, "text/plain")
        {
        }

        public BadRequestResult(object content) : this(null, "application/json")
        {
            Content = JsonConvert.SerializeObject(content);
        }

        public BadRequestResult(string content, string mediaType)
        {
            Content = content;
            MediaType = mediaType;
        }

        public string Content { get; }

        public string MediaType { get; }

        public Task<HttpResponseMessage> ExecuteAsync(CancellationToken cancellationToken)
        {
            var resp = new HttpResponseMessage(HttpStatusCode.BadRequest);
            resp.Content = new StringContent(Content, null, MediaType);
            return Task.FromResult(resp);
        }
    }
}

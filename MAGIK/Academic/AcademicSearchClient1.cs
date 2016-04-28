// Some portion of the code is derived from Microsoft.ProjectOxford.Vision
//
// Copyright (c) Microsoft Corporation
// All rights reserved.
//
// MIT License:
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED ""AS IS"", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.IO;
using System.Net;
using Microsoft.Contests.Bop.Participants.Magik.Academic.Contract;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.Contests.Bop.Participants.Magik.Academic
{
    partial class AcademicSearchClient
    {
        /// <summary>
        /// The default resolver
        /// </summary>
        private CamelCasePropertyNamesContractResolver _defaultResolver = new CamelCasePropertyNamesContractResolver();

        #region the json client

        /// <summary>
        /// Process the exception happened on rest call.
        /// </summary>
        /// <param name="exception">Exception object.</param>
        private void HandleException(Exception exception)
        {
            var webException = exception as WebException;
            if (webException?.Response == null) return;
            if (!webException.Response.ContentType.ToLower().Contains("application/json"))
                return;
            Stream stream = null;
            try
            {
                stream = webException.Response.GetResponseStream();
                if (stream != null)
                {
                    string errorObjectString;
                    using (var reader = new StreamReader(stream))
                    {
                        stream = null;
                        errorObjectString = reader.ReadToEnd();
                    }
                    var errorCollection = JsonConvert.DeserializeObject<ClientErrorContainer>(errorObjectString);
                    if (errorCollection != null)
                    {
                        throw new ClientException(errorCollection.Error);
                    }
                }
            }
            finally
            {
                stream?.Dispose();
                throw exception;
            }
        }
            #endregion
        }
}

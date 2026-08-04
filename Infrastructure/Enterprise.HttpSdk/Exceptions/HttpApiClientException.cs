using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Enterprise.HttpSdk.Exceptions
{
    public class HttpApiClientException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string? ResponseContent { get; }

        public HttpApiClientException(HttpStatusCode statusCode, string message, string? responseContent = null)
            : base(message)
        {
            StatusCode = statusCode;
            ResponseContent = responseContent;
        }
    }
}

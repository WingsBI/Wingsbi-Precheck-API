using Microsoft.AspNetCore.Http;

namespace Precheck.Agent
{
    public static class AgentSessionHeader
    {
        public const string Name = "X-Session-Id";

        public static bool TryGet(IHttpContextAccessor http, out Guid sessionId)
        {
            var value = http.HttpContext?.Request.Headers[Name].ToString();
            return Guid.TryParse(value, out sessionId);
        }
    }
}

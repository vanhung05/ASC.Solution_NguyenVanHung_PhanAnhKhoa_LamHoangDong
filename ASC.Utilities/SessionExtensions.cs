using Newtonsoft.Json;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace ASC.Utilities
{
    public static class SessionExtensions
    {
        public static void SetSession(this ISession session, string key, object value)
        {
            session.Set(key, Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(value)));
        }

        public static T GetSession<T>(this ISession session, string key)
        {
            byte[] value;
            if (session.TryGetValue(key, out value))
            {
                return JsonConvert.DeserializeObject<T>(Encoding.ASCII.GetString(value));
            }
            else
            {
                return default(T);
            }
        }
    }
}
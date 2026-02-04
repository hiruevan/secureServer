using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SecureServerCommand
{
    public class Token
    {
        [JsonPropertyName("session_id")]
        public string Id { get; set; }

        [JsonPropertyName("value")]
        public string Value { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("login_time")]
        public string LoginTime { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }
    }
}

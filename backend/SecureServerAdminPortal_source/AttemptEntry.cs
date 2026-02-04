using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SecureServerCommand
{
    public class AttemptEntry
    {
        [JsonPropertyName("username")]
        public string User { get; set; }

        [JsonPropertyName("timestamp")]
        public string Time { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerLogViewer
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string LogLevel { get; set; }
        public string Message { get; set; }
        public string IpAddress { get; set; }
        public string HttpMethod { get; set; }
        public string Endpoint { get; set; }
        public string HttpVersion { get; set; }
        public string StatusCode { get; set; }
        public string StatusText { get; set; }
        public string FullLine { get; set; }
        public Color LevelColor { get; set; }
        public bool HasProtocolInfo { get; set; }
        public string LevelFilter { get; set; }
    }
}

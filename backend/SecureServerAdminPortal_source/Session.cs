using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SecureServerCommand
{
    public class Session
    {
        public string Id;
        public string Name;

        public Session(string id = "null", string name = "Unknown")
        {
            Id = id;
            Name = name;
        }

        public override string ToString()
        {
            return $"User: {Name}, Token Value: {Id}";
        }
    }
}

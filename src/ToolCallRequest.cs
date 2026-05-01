using System.Collections.Generic;

namespace HAL9000
{
    internal sealed class ToolCallRequest
    {
        public string Name;
        public Dictionary<string, object> Arguments;

        public ToolCallRequest(string name, Dictionary<string, object> arguments)
        {
            Name = name;
            Arguments = arguments ?? new Dictionary<string, object>();
        }
    }
}

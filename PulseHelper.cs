using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVLink_Server
{
    public class PulseHelper
    {

        public string Channel_Set(string clientId, string appId, string channel, int pulse)
        {
            PulseJson json = new()
            {
                clientId = clientId,
                targetId = appId
            };
            string pulsejson = JsonConvert.SerializeObject(JlWebSocketServer.pulseDic[pulse]);
            string pulseMessage = $"pulse-{channel}:{pulsejson}";
            json.message = pulseMessage;
            string result = JsonConvert.SerializeObject(json);
            return result;
        }
    }

    /// <summary>
    /// 强度Json
    /// </summary>
    public class PulseJson
    {
        public string type { get; set; } = "msg";
        public string clientId { get; set; } = "";
        public string targetId { get; set; } = "";
        public string message { get; set; } = "";
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DVLink_Server
{
    /// <summary>
    /// 波形强度辅助类
    /// </summary>
    public class PulseHelper
    {
        public string GetPulseJson(string clientId, string appId, string channel, int pulse)
        {
            PulseJson json = new()
            {
                clientId = clientId,
                targetId = appId
            };
            string pulseJson = JsonConvert.SerializeObject(JlWebSocketServer.pulseDic[pulse]);
            string pulseMessage = $"pulse-{channel}:{pulseJson}";
            json.message = pulseMessage;
            string result = JsonConvert.SerializeObject(json);
            return result;
        }

        public string GetPulseJson(string clientId, string appId, string channel, string pulseJson)
        {
            PulseJson json = new()
            {
                clientId = clientId,
                targetId = appId
            };
            string pulseMessage = $"pulse-{channel}:{pulseJson}";
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

using Newtonsoft.Json;

namespace DVLink_Server
{
    /// <summary>
    /// 强度辅助类
    /// </summary>
    public class StrengthHelper
    {
        /// <summary>
        /// 通道强度设置
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="appId"></param>
        /// <param name="channel"></param>
        /// <param name="strength"></param>
        /// <returns></returns>
        public string Channel_Set(string clientId, string appId, int channel, int strength)
        {
            StrengthJson json = new()
            {
                clientId = clientId,
                targetId = appId
            };
            string strengthMessage = $"strength-{channel}+2+{strength}";
            json.message = strengthMessage;
            string result = JsonConvert.SerializeObject(json);
            return result;
        }
        /// <summary>
        /// 通道强度增加
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="appId"></param>
        /// <param name="channel"></param>
        /// <param name="strength"></param>
        /// <returns></returns>
        public string Channel_Add(string clientId, string appId, int channel, int strength)
        {
            StrengthJson json = new()
            {
                clientId = clientId,
                targetId = appId
            };
            string strengthMessage = $"strength-{channel}+1+{strength}";
            json.message = strengthMessage;
            string result = JsonConvert.SerializeObject(json);
            return result;
        }
        /// <summary>
        /// 通道强度减少
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="appId"></param>
        /// <param name="channel"></param>
        /// <param name="strength"></param>
        /// <returns></returns>
        public string Channel_Deduct(string clientId, string appId, int channel, int strength)
        {
            StrengthJson json = new()
            {
                clientId = clientId,
                targetId = appId
            };
            string strengthMessage = $"strength-{channel}+0+{strength}";
            json.message = strengthMessage;
            string result = JsonConvert.SerializeObject(json);
            return result;
        }
        /// <summary>
        /// 强度Json
        /// </summary>
        public class StrengthJson
        {
            public string type { get; set; } = "msg";
            public string clientId { get; set; } = "";
            public string targetId { get; set; } = "";
            public string message { get; set; } = "";
        }
    }
}

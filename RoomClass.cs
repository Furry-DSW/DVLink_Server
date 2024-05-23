using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace DVLink_Server
{
    /// <summary>
    /// 房间类
    /// </summary>
    public class RoomClass
    {
        public RoomClass()
        {
            roomClients.TryAdd(0, "");
            roomClients.TryAdd(1, "");
            roomClients.TryAdd(2, "");
            roomClients.TryAdd(3, "");
        }
        /// <summary>
        /// 房间锁定
        /// </summary>
        public bool isLocked = false;
        /// <summary>
        /// 房间内客户端字典
        /// </summary>
        public ConcurrentDictionary<int, string> roomClients = new();
        /// <summary>
        /// 房间内客户端数量
        /// </summary>
        public int roomPlayerNum;
        /// <summary>
        /// 添加客户端
        /// </summary>
        /// <param name="clientId"></param>
        public void AddClient(string clientId)
        {
            foreach (var item in roomClients)
            {
                if (item.Value == "")
                {
                    roomClients[item.Key] = clientId;
                    roomPlayerNum++;
                    break;
                }
            }
        }

        /// <summary>
        /// 删除客户端
        /// </summary>
        /// <param name="clientId"></param>
        public void RemoveClient(string clientId)
        {
            foreach (var item in roomClients)
            {
                if (item.Value == clientId)
                {
                    roomClients[item.Key] = "";
                    roomPlayerNum--;
                    break;
                }
            }
        }
        /// <summary>
        /// 改变房间状态
        /// </summary>
        public void ChangeRoomState()
        {
            isLocked = !isLocked;
        }
        /// <summary>
        /// 发送状态给房间内所有客户端
        /// </summary>
        public async void SendStateToAllClient()
        {
            List<JlWebSocketServer.AppState> appStates = new();
            foreach (var item in roomClients)
            {
                if (item.Value != "")
                    appStates.Add(JlWebSocketServer.appState[item.Value]);
                else
                    appStates.Add(new JlWebSocketServer.AppState());
            }
            AllAppState json = new()
            {
                type = "appState",
                message = appStates
            };
            string result = JsonConvert.SerializeObject(json);
            await SendToAllClient(roomClients, result);
        }
        /// <summary>
        /// 发送信息给房间内所有客户端
        /// </summary>
        /// <param name="roomClients"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private static async Task SendToAllClient(ConcurrentDictionary<int, string> roomClients, string message)
        {
            List<WebSocket> webSockets = new();
            foreach(var item in roomClients)
            {
                if (item.Value != "")
                    webSockets.Add(JlWebSocketServer.clients[item.Value]);
            }
            foreach (var item in webSockets)
            {
                await JlWebSocketServer.SendMessage(item, message);
            }
        }
        /// <summary>
        /// 发送房间状态给所有客户端
        /// </summary>
        public async void SendRoomStateToAllClient()
        {
            JlWebSocketServer.MessageJson json = new()
            {
                type = "roomState",
                message = isLocked.ToString()
            };
            string result = JsonConvert.SerializeObject(json);
            await SendToAllClient(roomClients, result);
        }
    }
    /// <summary>
    /// 所有APP状态
    /// </summary>
    public class AllAppState
    {
        public string type = "";
        public string clientId = "";
        public string targetId = "";
        public List<JlWebSocketServer.AppState> message = new();
    }



}

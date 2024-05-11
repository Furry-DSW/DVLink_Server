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
    public class RoomClass
    {
        /// <summary>
        /// 房间锁定
        /// </summary>
        public bool isLocked = false;
        /// <summary>
        /// 房间内客户端字典
        /// </summary>
        public List<string> roomClients = [];
        /// <summary>
        /// 房间内客户端数量
        /// </summary>
        public int roomPlayerNum;
        /// <summary>
        /// 发送信息给指定客户端
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private static async Task SendToClient(string clientId, string message)
        {
            WebSocket socket = JlWebSocketServer.clients[clientId];
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            //Console.WriteLine($"Sent: {message}");
        }
        /// <summary>
        /// 添加客户端
        /// </summary>
        /// <param name="clientId"></param>
        public void AddClient(string clientId)
        {
            roomClients.Add(clientId);
            roomPlayerNum++;
        }
        /// <summary>
        /// 删除客户端
        /// </summary>
        /// <param name="clientId"></param>
        public void RemoveClient(string clientId)
        {
            roomClients.Remove(clientId);
            roomPlayerNum--;
        }

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
                appStates.Add(JlWebSocketServer.appState[item]);
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
        private static async Task SendToAllClient(List<string> roomClients, string message)
        {
            List<WebSocket> webSockets = new();
            foreach(var item in roomClients)
            {
                webSockets.Add(JlWebSocketServer.clients[item]);
            }
            foreach (var item in webSockets)
            {
                await JlWebSocketServer.SendMessage(item, message);
            }
        }

    }
    public class AmmoInfo
    {
        public int playerId;
        public int strength;
        public int time;
    }
    public class AllAppState
    {
        public string type = "";
        public string clientId = "";
        public string targetId = "";
        public List<JlWebSocketServer.AppState> message = new();
    }



}

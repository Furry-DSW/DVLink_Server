using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Channels;
using System.Data;

namespace DVLink_Server
{
    public class JlWebSocketServer
    {
        /// <summary>
        /// 波形字典
        /// </summary>
        public static Dictionary<int, string[]> pulseDic = new Dictionary<int, string[]>
        {
            {1,["0A0A0A0A03090C0F","0A0A0A0A1215181B","0A0A0A0A1E212427","0A0A0A0A2A2D3033","0A0A0A0A36393C3F","0A0A0A0A4245484B","0A0A0A0A4E515457","0A0A0A0A5A5D6063","0A0A0A0A64646464","0A0A0A0A00000000"]},
            {2,["0A0A0A0A64646464","0A0A0A0A00000000","0A0A0A0A64646464","0A0A0A0A00000000","0A0A0A0A64646464","0A0A0A0A00000000","0A0A0A0A64646464","0A0A0A0A00000000","0A0A0A0A64646464","0A0A0A0A00000000"]},
            {3,["0A0A0A0A14141414","0A0A0A0A00000000","0A0A0A0A28282828","0A0A0A0A00000000","0A0A0A0A3C3C3C3C","0A0A0A0A00000000","0A0A0A0A50505050","0A0A0A0A00000000","0A0A0A0A64646464","0A0A0A0A00000000"]},
            {4,["1A1B1C1D64646464","1E1F202164646464","2223242564646464","2627282964646464","2A2B2C2D64646464","2E2F303164646464","3233343564646464","3637383964646464","3A3B3C3D64646464","3E3F404164646464"]}
        };
        /// <summary>
        /// 客户端字典
        /// </summary>
        public static ConcurrentDictionary<string, WebSocket> clients = new();
        /// <summary>
        /// 客户端-APP-UUID绑定字典
        /// </summary>
        public static ConcurrentDictionary<string, string> clientAppDic = new();
        /// <summary>
        /// 房间字典
        /// </summary>
        public static ConcurrentDictionary<string, RoomClass> roomDic = new();
        /// <summary>
        /// App状态字典
        /// </summary>
        public static ConcurrentDictionary<string, AppState> appState = new();
        /// <summary>
        /// 客户端与房间绑定字典
        /// </summary>
        public static ConcurrentDictionary<string, string> clientRoom = new();
        /// <summary>
        /// 客户端波形定时器字典
        /// </summary>
        public static ConcurrentDictionary<string, System.Timers.Timer> clientTimer = new();
        /// <summary>
        /// SocketSend字典
        /// </summary>
        public static ConcurrentDictionary<WebSocket, SocketSend> wsSendDic = new();

        private static async Task Start(string ip, int port)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add($"http://{ip}:{port}/");
            listener.Start();
            Console.WriteLine($"开始监听端口:{port}");
            while (true)
            {
                HttpListenerContext context = await listener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    ProcessWebSocketRequest(context);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }
        /// <summary>
        /// 接受到消息后的处理
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        private static async Task ProcessReceivedMessage(string uuid, WebSocket socket, string message)
        {
            string from = "";
            // 处理接收到的消息
            JObject json;
            try
            {
                //解析消息
                json = JObject.Parse(message);
            }
            catch (JsonReaderException)
            {
                // 解析失败，返回错误消息
                string res = "{ type: 'msg', clientId: \"\", targetId: \"\", message: '403' }";
                await SendMessage(socket, res);
                return;
            }
            //获取消息中的clientId和targetId
            string clientId = json["clientId"]?.ToString() ?? "";
            string targetId = json["targetId"]?.ToString() ?? "";
            string type = json["type"]?.ToString() ?? "";
            if (uuid != clientId && uuid != targetId)
            {
                // 消息中的clientId和targetId与当前连接的客户端ID不匹配，返回错误消息
                string res = "{ type: 'msg', clientId: \"\", targetId: \"\", message: '非法用户' }";
                await SendMessage(socket, res);
                return;
            }
            //判断绑定字典中是否存在客户端
            if (clientAppDic.ContainsKey(clientId))
            {
                //判断绑定关系是否正确
                if (clientAppDic[clientId] != targetId)
                {
                    string res = "{ type: 'msg', clientId: \"\", targetId: \"\", message: '非法用户' }";
                    await SendMessage(socket, res);
                    return;
                }
            }
            //不存在绑定关系
            else
            {
                //判断消息类型是否为绑定
                if (type == "bind")
                {
                    //绑定客户端
                    BindClient(socket, clientId, targetId);
                }
                else
                {
                    string res = "{ type: 'msg', clientId: \"\", targetId: \"\", message: '未绑定APP' }";
                    await SendMessage(socket, res);
                    return;
                }
            }
            //判断消息来源
            if (uuid == clientId)
            {
                from = "client";
            }
            else
            {
                from = "app";
            }
            //获取消息中的type和message
            string msg = json["message"]?.ToString() ?? "";
            //来自客户端
            if (from == "client")
            {
                switch (type)
                {
                    //客户端进入房间
                    case "RoomEnter":
                        RoomEnter(clientId, msg);
                        string playerName = json["playerName"]?.ToString() ?? "";
                        appState[clientId].playerName = playerName;
                        roomDic[clientRoom[clientId]].SendStateToAllClient();
                        break;
                    //客户端退出房间
                    case "RoomExit":
                        RoomExit(clientId);
                        break;
                    case "SetClientStrength":
                        SetClientStrength(json, uuid);
                        break;
                    case "AddClientStrength":
                        AddClientStrength(json, uuid);
                        break;
                    case "SetClientPulse":
                        SetClientPulse(json, uuid);
                        break;
                    case "ResetClientPulse":
                        ResetClientPulse(json, uuid);
                        break;
                    case "ChangeRoomState":
                        ChangeRoomState(uuid);
                        break;
                    case "GesturePulse":
                        GesturePulse(json, uuid);
                        break;
                    default:
                        break;
                }
            }
            //来自APP
            else
            {
                switch (type)
                {
                    //APP发送消息给客户端
                    case "msg":
                        //如果客户端未加入房间
                        if (!clientRoom.ContainsKey(clientId))
                        {
                            updateAppState(clientId, targetId, msg);
                        }
                        else
                            //如果客户端加入房间则发送给房间内所有客户端
                            try
                            {
                                updateAppState(clientId, targetId, msg);
                                roomDic[clientRoom[clientId]].SendStateToAllClient();
                            }
                            catch (Exception ex)
                            { Console.WriteLine(ex.Message); }
                        break;
                    default:
                        break;
                }
            }
        }
        /// <summary>
        /// websocket请求处理
        /// </summary>
        /// <param name="context"></param>
        private static async void ProcessWebSocketRequest(HttpListenerContext context)
        {
            WebSocketContext? webSocketContext = null;
            Guid uuid = Guid.NewGuid();
            string clientId = uuid.ToString();
            try
            {
                webSocketContext = await context.AcceptWebSocketAsync(null);
                Console.WriteLine($"客户端{uuid}已连接");

                //客户端连接成功后，将其添加到clients字典中
                WebSocket socket = webSocketContext.WebSocket;
                clients.TryAdd(clientId, socket);
                wsSendDic.TryAdd(socket, new SocketSend());
                await SendMessage(socket, $"{{\"type\":\"bind\",\"clientId\":\"{clientId}\",\"message\":\"targetId\",\"targetId\":\"\"}}");

                // 开始接收并处理消息
                await ReceiveMessages(clientId, socket);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"连接错误: {ex}");
            }
            finally
            {
                ///断开连接后，将其从clients字典中移除
                string RemoveId = "";
                foreach (var item in clientAppDic)
                {
                    if (item.Key == clientId)
                    {
                        WebSocket ws = clients[item.Value];
                        MessageJson messageJson = new MessageJson();
                        messageJson.type = "break";
                        messageJson.clientId = "";
                        messageJson.targetId = "";
                        messageJson.message = "209";
                        //给APP发送断开连接消息
                        string res = JsonConvert.SerializeObject(messageJson);
                        await SendMessage(ws, res);
                        RemoveId = item.Key;
                        break;
                    }
                    if (item.Value == clientId)
                    {
                        WebSocket ws = clients[item.Key];
                        MessageJson messageJson = new MessageJson();
                        messageJson.type = "break";
                        messageJson.clientId = "";
                        messageJson.targetId = "";
                        messageJson.message = "209";
                        //给客户端发送断开连接消息
                        string res = JsonConvert.SerializeObject(messageJson);
                        if (clientRoom.ContainsKey(item.Key))
                        {
                            RoomExit(item.Key);
                        }

                        await SendMessage(ws, res);
                        appState[item.Key].state = "offline";
                        RemoveId = item.Key;
                        break;
                    }
                }
                if (clientRoom.ContainsKey(clientId))
                {
                    RoomExit(clientId);
                }
                if (RemoveId != "")
                {
                    //断开连接后，将其从clientAppDic字典中移除
                    clientAppDic.TryRemove(RemoveId, out _);
                    appState.TryRemove(RemoveId, out _);
                    foreach (var item in clientTimer)
                    {
                        if (item.Key.Contains(RemoveId))
                        {
                            item.Value.Stop();
                            clientTimer.TryRemove(item.Key, out _);
                        }
                    }
                }
                if (webSocketContext != null)
                    webSocketContext.WebSocket.Dispose();
                wsSendDic.TryRemove(clients[clientId], out _);
                clients.TryRemove(clientId, out _);
                Console.WriteLine($"{clientId} 离线");
            }
        }
        /// <summary>
        /// 接收消息
        /// </summary>
        /// <param name="uuid"></param>
        /// <param name="socket"></param>
        /// <returns></returns>
        private static async Task ReceiveMessages(string uuid, WebSocket socket)
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"接收信息:{uuid}-{receivedMessage}");

                        // 处理接收到的消息
                        try
                        {
                            await ProcessReceivedMessage(uuid, socket, receivedMessage);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"{ex.Message}");
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("连接被客户端断开");
                        break;
                    }
                }
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"连接错误: {ex.Message}");
            }
            finally
            {
                if (socket.State != WebSocketState.Closed)
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
            }
        }
        /// <summary>
        /// 设置客户端强度
        /// </summary>
        /// <param name="json"></param>
        /// <param name="uuid"></param>
        private static async void SetClientStrength(JObject json, string uuid)
        {
            try
            {
                if (!clientRoom.ContainsKey(uuid)) return;
                RoomClass room = roomDic[clientRoom[uuid]];
                int controlId = Convert.ToInt16(json["controlId"]);
                if (controlId < 0 || controlId > room.roomPlayerNum - 1) return;
                string controlClient = room.roomClients[controlId];
                string controlApp = clientAppDic[controlClient];
                StrengthHelper strengthHelper = new StrengthHelper();
                string strengthMessage = strengthHelper.Channel_Set(controlClient, controlApp, Convert.ToInt16(json["channel"]), Convert.ToInt16(json["message"]));
                WebSocket app = clients[controlApp];
                await SendMessage(app, strengthMessage);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        /// <summary>
        /// 一键开火(自定义持续时间)
        /// </summary>
        /// <param name="json"></param>
        /// <param name="uuid"></param>
        private static async void AddClientStrength(JObject json, string uuid)
        {
            try
            {
                if (!clientRoom.ContainsKey(uuid)) return;
                RoomClass room = roomDic[clientRoom[uuid]];
                int controlId = Convert.ToInt16(json["controlId"]);
                int time = Convert.ToInt16(json["time"]);
                if (controlId < 0 || controlId > room.roomPlayerNum - 1) return;
                string controlClient = room.roomClients[controlId];
                string controlApp = clientAppDic[controlClient];
                AppState State = appState[controlClient];
                int channel = Convert.ToInt16(json["channel"]);
                if (channel == 1)
                {
                    if (State.aBusy) return;
                    else State.aBusy = true;
                }
                if (channel == 2)
                {
                    if (State.bBusy) return;
                    else State.bBusy = true;
                }
                StrengthHelper strengthHelper = new StrengthHelper();
                int strength = Convert.ToInt16(json["message"]);
                switch (channel)
                {
                    case 1:
                        if (Convert.ToInt16(State.aNow) + strength > Convert.ToInt16(State.aLimit))
                            strength = Convert.ToInt16(State.aLimit) - Convert.ToInt16(State.aNow);
                        break;
                    case 2:
                        if (Convert.ToInt16(State.bNow) + strength > Convert.ToInt16(State.bLimit))
                            strength = Convert.ToInt16(State.bLimit) - Convert.ToInt16(State.bNow);
                        break;
                    default:
                        return;
                }
                string addMessage = strengthHelper.Channel_Add(controlClient, controlApp, channel, strength);
                string deductMessage = strengthHelper.Channel_Deduct(controlClient, controlApp, channel, strength);
                WebSocket app = clients[controlApp];
                await SendMessage(app, addMessage);
                await Task.Delay(time * 1000);
                await SendMessage(app, deductMessage);
                if (channel == 1) State.aBusy = false;
                if (channel == 2) State.bBusy = false;
            }
            catch
            (Exception ex)
            {
                RoomClass room = roomDic[clientRoom[uuid]];
                int controlId = Convert.ToInt16(json["controlId"]);
                string controlClient = room.roomClients[controlId];
                AppState State = appState[controlClient];
                State.aBusy = false;
                State.bBusy = false;
                Console.WriteLine(ex.Message);
            }
        }
        /// <summary>
        /// 设置客户端波形
        /// </summary>
        /// <param name="json"></param>
        /// <param name="uuid"></param>
        private static void SetClientPulse(JObject json, string uuid)
        {
            try
            {
                if (!clientRoom.ContainsKey(uuid)) return;
                RoomClass room = roomDic[clientRoom[uuid]];
                int controlId = Convert.ToInt16(json["controlId"]);
                if (controlId < 0 || controlId > room.roomPlayerNum - 1) return;
                string controlClient = room.roomClients[controlId];
                string controlApp = clientAppDic[controlClient];
                string channel = json["channel"]!.ToString();
                int pulse = Convert.ToInt16(json["message"]);
                if (channel == "A")
                {
                    appState[controlClient].aPulse = pulse.ToString();
                    room.SendStateToAllClient();
                }
                else if (channel == "B")
                {
                    appState[controlClient].bPulse = pulse.ToString();
                    room.SendStateToAllClient();
                }
                if (pulse == 0)
                {
                    RemovePulseTimer(controlClient, channel);
                    return;
                }
                PulseHelper pulseHelper = new PulseHelper();
                int time = pulseDic[pulse].Length;
                string pulseMessage = pulseHelper.GetPulseJson(controlClient, controlApp, channel, pulse);
                WebSocket app = clients[controlApp];
                AddPulseTimer(controlClient, controlApp, channel, time, pulseMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        /// <summary>
        /// 手势控制波形
        /// </summary>
        /// <param name="json"></param>
        /// <param name="uuid"></param>
        private static async void GesturePulse(JObject json,string uuid)
        {
            try
            {
                if (!clientRoom.ContainsKey(uuid)) return;
                RoomClass room = roomDic[clientRoom[uuid]];
                int controlId = Convert.ToInt16(json["controlId"]);
                if (controlId < 0 || controlId > room.roomPlayerNum - 1) return;
                string controlClient = room.roomClients[controlId];
                string channel = json["channel"]!.ToString();
                if (channel == "A")
                {
                    if (appState[controlClient].aPulse != "0") return;
                }
                else if (channel == "B")
                {
                    if (appState[controlClient].bPulse != "0") return;
                }
                string pulseJson = json["message"]!.ToString();
                if (pulseJson == "") return;
                string controlApp = clientAppDic[controlClient];
                PulseHelper pulseHelper = new PulseHelper();
                string pulseMessage = pulseHelper.GetPulseJson(controlClient, controlApp, channel, pulseJson);
                WebSocket app = clients[controlApp];
                await SendMessage(app, pulseMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// 重置客户端波形
        /// </summary>
        /// <param name="json"></param>
        /// <param name="uuid"></param>
        private static void ResetClientPulse(JObject json, string uuid)
        {
            try
            {
                if (!clientRoom.ContainsKey(uuid)) return;
                RoomClass room = roomDic[clientRoom[uuid]];
                int controlId = Convert.ToInt16(json["controlId"]);
                if (controlId < 0 || controlId > room.roomPlayerNum - 1) return;
                string controlClient = room.roomClients[controlId];
                RemovePulseTimer(controlClient);
                room.SendStateToAllClient();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        /// <summary>
        /// 退出房间
        /// </summary>
        /// <param name="clientId"></param>
        private static async void RoomExit(string clientId)
        {
            try
            {
                if (!clientRoom.ContainsKey(clientId)) return;
                string RoomId = clientRoom[clientId];
                clientRoom.TryRemove(clientId, out _);
                if (roomDic.ContainsKey(RoomId))
                {
                    WebSocket webSocket = clients[clientId];
                    MessageJson messageJson = new()
                    {
                        type = "RoomExit",
                        clientId = clientId,
                        targetId = "",
                        message = "200"
                    };
                    string messenger = JsonConvert.SerializeObject(messageJson);
                    roomDic[RoomId].RemoveClient(clientId);
                    roomDic[RoomId].SendStateToAllClient();
                    await SendMessage(webSocket, messenger);
                    Console.WriteLine($"房间{RoomId}内客户端数量:{roomDic[RoomId].roomPlayerNum}");
                    Console.WriteLine($"房间{RoomId}内客户端:{string.Join(",", roomDic[RoomId].roomClients)}");
                    if (roomDic[RoomId].roomPlayerNum == 0)
                    {
                        roomDic.TryRemove(RoomId, out _);
                        Console.WriteLine($"房间{RoomId}已解散");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        /// <summary>
        /// 进入房间
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="RoomId"></param>
        private static async void RoomEnter(string clientId, string RoomId)
        {
            try
            {
                if (clientRoom.ContainsKey(clientId)) return;
                clientRoom.TryAdd(clientId, RoomId);
                if (!roomDic.ContainsKey(RoomId))
                {
                    RoomClass room = new RoomClass();
                    roomDic.TryAdd(RoomId, room);
                    WebSocket webSocket = clients[clientId];
                    MessageJson messageJson = new()
                    {
                        type = "RoomEnter",
                        clientId = clientId,
                        targetId = "",
                        message = "200"
                    };
                    string messenger = JsonConvert.SerializeObject(messageJson);
                    room.AddClient(clientId);
                    await SendMessage(webSocket, messenger);
                    Console.WriteLine($"房间{RoomId}创建成功");
                    Console.WriteLine($"房间{RoomId}内客户端数量:{room.roomPlayerNum}");
                    Console.WriteLine($"房间{RoomId}内客户端:{string.Join(",", room.roomClients)}");
                }
                else
                {
                    if (roomDic[RoomId].isLocked) return;
                    if (roomDic[RoomId].roomPlayerNum >= 4)
                        return;
                    WebSocket webSocket = clients[clientId];
                    MessageJson messageJson = new()
                    {
                        type = "RoomEnter",
                        clientId = clientId,
                        targetId = "",
                        message = "200"
                    };
                    string messenger = JsonConvert.SerializeObject(messageJson);
                    roomDic[RoomId].AddClient(clientId);
                    await SendMessage(webSocket, messenger);
                    Console.WriteLine($"房间{RoomId}内客户端数量:{roomDic[RoomId].roomPlayerNum}");
                    Console.WriteLine($"房间{RoomId}内客户端:{string.Join(",", roomDic[RoomId].roomClients)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        /// <summary>
        /// 改变房间状态(上锁)
        /// </summary>
        /// <param name="clientId"></param>
        private static void ChangeRoomState(string clientId)
        {
            if (!clientRoom.ContainsKey(clientId)) return;
            if (!roomDic.ContainsKey(clientRoom[clientId])) return;
            roomDic[clientRoom[clientId]].ChangeRoomState();
            roomDic[clientRoom[clientId]].SendRoomStateToAllClient();
        }
        /// <summary>
        /// 绑定客户端
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="clientId"></param>
        /// <param name="targetId"></param>
        private async static void BindClient(WebSocket socket, string clientId, string targetId)
        {
            try
            {
                //判断客户端和目标客户端是否存在
                if (clients.Keys.Contains(clientId) && clients.Keys.Contains(targetId))
                {
                    //判断客户端和目标客户端是否已经绑定
                    if (!clientAppDic.Keys.Contains(clientId) && !clientAppDic.Keys.Contains(targetId) && !clientAppDic.Values.Contains(clientId) && !clientAppDic.Values.Contains(targetId))
                    {
                        clientAppDic.TryAdd(clientId, targetId);
                        appState.TryAdd(clientId, new AppState());
                        appState[clientId].state = "online";
                        WebSocket client = clients[clientId];
                        WebSocket target = clients[targetId];
                        MessageJson bindMessage = new MessageJson();
                        bindMessage.type = "bind";
                        bindMessage.clientId = clientId;
                        bindMessage.targetId = targetId;
                        bindMessage.message = "200";
                        string res = JsonConvert.SerializeObject(bindMessage);
                        await SendMessage(client, res);
                        await SendMessage(target, res);
                    }
                    else
                    {
                        WebSocket client = clients[clientId];
                        WebSocket target = clients[targetId];
                        MessageJson bindMessage = new MessageJson();
                        bindMessage.type = "bind";
                        bindMessage.clientId = "";
                        bindMessage.targetId = "";
                        bindMessage.message = "400";
                        string res = JsonConvert.SerializeObject(bindMessage);
                        await SendMessage(client, res);
                        await SendMessage(target, res);
                    }
                }
                else
                {
                    MessageJson bindMessage = new MessageJson();
                    bindMessage.type = "bind";
                    bindMessage.clientId = "";
                    bindMessage.targetId = "";
                    bindMessage.message = "401";
                    string res = JsonConvert.SerializeObject(bindMessage);
                    await SendMessage(socket, res);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        /// <summary>
        /// 更新APP状态
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="appId"></param>
        /// <param name="msg"></param>
        private static void updateAppState(string clientId, string appId, string msg)
        {
            try
            {
                if (msg == "") return;
                if (clientAppDic[clientId] != appId) return;
                if (msg.Contains("strength-"))
                {
                    string strengthString = msg.Replace("strength-", "");
                    string[] strengthArray = strengthString.Split("+");
                    appState[clientId].aNow = strengthArray[0];
                    appState[clientId].bNow = strengthArray[1];
                    appState[clientId].aLimit = strengthArray[2];
                    appState[clientId].bLimit = strengthArray[3];
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        /// <summary>
        /// 添加波形定时器
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="appId"></param>
        /// <param name="time"></param>
        /// <param name="message"></param>
        private static async void AddPulseTimer(string clientId, string appId, string channel, int time, string message)
        {
            string key = clientId + $"+{channel}";
            WebSocket app = clients[appId];
            await SendMessage(app, message);
            if (!clientTimer.ContainsKey(key))
            {
                System.Timers.Timer timer = new System.Timers.Timer(time * 100);
                timer.Elapsed += async (sender, e) =>
                {
                    await SendMessage(app, message);
                };
                timer.Start();
                clientTimer.TryAdd(key, timer);
            }
            else
            {
                clientTimer[key].Stop();
                System.Timers.Timer timer = new System.Timers.Timer(time * 100);
                timer.Elapsed += async (sender, e) =>
                {
                    await SendMessage(app, message);
                };
                timer.Start();
                clientTimer[key] = timer;
            }
        }




        /// <summary>
        /// 移除波形定时器
        /// </summary>
        /// <param name="clientId"></param>
        private static async void RemovePulseTimer(string clientId)
        {
            foreach (var item in clientTimer)
            {
                if (item.Key.Contains(clientId))
                {
                    item.Value.Stop();
                    clientTimer.TryRemove(item.Key, out _);
                }
            }
            appState[clientId].aPulse = "0";
            appState[clientId].bPulse = "0";
            MessageJson messageJsonA = new()
            {
                type = "msg",
                clientId = clientId,
                targetId = clientAppDic[clientId],
                message = "clear-1"
            };
            MessageJson messageJsonB = new()
            {
                type = "msg",
                clientId = clientId,
                targetId = clientAppDic[clientId],
                message = "clear-2"
            };
            string messageA = JsonConvert.SerializeObject(messageJsonA);
            string messageB = JsonConvert.SerializeObject(messageJsonB);
            await SendMessage(clients[clientAppDic[clientId]], messageA);
            await SendMessage(clients[clientAppDic[clientId]], messageB);
        }
        /// <summary>
        /// 移除指定波形定时器
        /// </summary>
        /// <param name="clientId"></param>
        /// <param name="channel"></param>
        private static async void RemovePulseTimer(string clientId, string channel)
        {
            string key = clientId + $"+{channel}";
            if (clientTimer.ContainsKey(key))
            {
                clientTimer[key].Stop();
                clientTimer.TryRemove(key, out _);
            }
            string clearMessage;
            if (channel == "A")
            {
                clearMessage = "clear-1";
            }
            else
            {
                clearMessage = "clear-2";
            }
            MessageJson messageJson = new()
            { 
                type = "msg",
                clientId = clientId,
                targetId = clientAppDic[clientId],
                message = clearMessage
            };
            string message = JsonConvert.SerializeObject(messageJson);
            await SendMessage(clients[clientAppDic[clientId]], message);
        }
        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static async Task SendMessage(WebSocket socket, string message)
        {
            try
            {
                SocketSend socketSend;
                if (wsSendDic.TryGetValue(socket, out socketSend!))
                {
                    await socketSend.SendMessageAsync(socket, message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        /// <summary>
        /// 主函数
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            string jsonString = File.ReadAllText("Config.json");
            JObject config = JObject.Parse(jsonString);
            int port = Convert.ToInt16(config["port"]);
            string ip = "*";
            Task task = Start(ip, port);
            // 创建定时器，指定间隔时间
            System.Timers.Timer heartBeat = new System.Timers.Timer(60000);
            // 设置定时器触发事件
            heartBeat.Elapsed += async (sender, e) =>
            {
                if (clients.Count > 0)
                {
                    Console.WriteLine($"客户端数量:{clients.Count},心跳发送时间:{DateTime.Now}");
                    foreach (var item in clients)
                    {
                        MessageJson heartBeatJson = new MessageJson();
                        heartBeatJson.type = "heartbeat";
                        heartBeatJson.clientId = item.Key;
                        if (clientAppDic.ContainsKey(item.Key))
                        {
                            heartBeatJson.targetId = clientAppDic[item.Key];
                        }
                        else
                        {
                            heartBeatJson.targetId = "";
                        }
                        heartBeatJson.message = "200";
                        string res = JsonConvert.SerializeObject(heartBeatJson);
                        await SendMessage(item.Value, res);
                    }
                }
            };
            // 开始定时器
            heartBeat.Start();
            task.Wait();
            Console.ReadLine();
        }
        /// <summary>
        /// 信息Json
        /// </summary>
        public class MessageJson
        {
            public string type = "";
            public string clientId = "";
            public string targetId = "";
            public string message = "";
        }
        /// <summary>
        /// APP状态类
        /// </summary>
        public class AppState
        {
            public string playerName = "";
            public string state = "offline";
            public bool aBusy = false;
            public bool bBusy = false;
            public string aNow = "0";
            public string bNow = "0";
            public string aLimit = "0";
            public string bLimit = "0";
            public string aPulse = "0";
            public string bPulse = "0";
        }
    }
}

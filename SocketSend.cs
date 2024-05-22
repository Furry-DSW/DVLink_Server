using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace DVLink_Server
{
    /// <summary>
    /// 发送信息锁类
    /// </summary>
    public class SocketSend
    {
        private readonly object _instanceLock = new object();
        /// <summary>
        /// 发送信息给指定客户端
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public Task SendMessageAsync(WebSocket socket, string message)
        {
            try
            {
                lock (_instanceLock)
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(message);
                    socket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return Task.CompletedTask;
            }
        }
    }
}

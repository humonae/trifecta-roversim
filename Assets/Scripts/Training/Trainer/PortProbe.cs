using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Training
{
    public static class PortProbe
    {
        public static Task<bool> IsListeningAsync(int port, int timeoutMilliseconds)
        {
            return Task.Run(() =>
            {
                try
                {
                    using (var client = new TcpClient())
                    {
                        Task connect = client.ConnectAsync(IPAddress.Loopback, port);
                        return connect.Wait(timeoutMilliseconds) && client.Connected;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }
    }
}

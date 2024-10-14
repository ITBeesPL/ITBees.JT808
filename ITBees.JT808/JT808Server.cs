using ITBees.Interfaces.Platforms;

namespace ITBees.JT808
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    public class JT808Server
    {
        private readonly int _port;
        private TcpListener listener;
        
        public JT808Server(IPlatformSettingsService platformSettingsService)
        {
            _port = Convert.ToInt32(platformSettingsService.GetSetting("JT808_port"));

            listener = new TcpListener(IPAddress.Any, _port);
        }

        public async Task StartListening()
        {
            listener.Start();
            Console.WriteLine($"JT808 Server is listening on port {_port}...");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("Device connected!");

                Task.Run(() => HandleClient(client));
            }
        }

        private async Task HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                byte[] receivedData = new byte[bytesRead];
                Array.Copy(buffer, 0, receivedData, 0, bytesRead);

                Console.WriteLine($"Received {bytesRead} bytes: {BitConverter.ToString(receivedData)}");

                // sample response to device
                byte[] response = CreateJT808Response();
                await stream.WriteAsync(response, 0, response.Length);
                Console.WriteLine("Response sent to the device.");
            }

            Console.WriteLine("Device disconnected.");
            client.Close();
        }

        private byte[] CreateJT808Response()
        {
            // Sample response
            string responseMessage = "JT808 response";
            return Encoding.ASCII.GetBytes(responseMessage);
        }

        public void Stop()
        {
            listener.Stop();
        }
    }
}

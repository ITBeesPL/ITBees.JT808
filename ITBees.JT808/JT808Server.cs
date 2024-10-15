using ITBees.Interfaces.Platforms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ITBees.JT808
{
    public class JT808Server : IJT808Server
    {
        private readonly int _port;
        private TcpListener listener;
        private readonly object fileLock = new object();
        private const string jsonFilePath = "receivedData.json";
        private SemaphoreSlim semaphore;
        public bool alreadyStarted { get; set; }

        public JT808Server(IPlatformSettingsService platformSettingsService)
        {
            _port = Convert.ToInt32(platformSettingsService.GetSetting("JT808_port"));
            semaphore = new SemaphoreSlim(Convert.ToInt32(platformSettingsService.GetSetting("JT808_maximum_parallel_clients")));
            listener = new TcpListener(IPAddress.Any, _port);
        }

        public async Task StartListening()
        {
            if (alreadyStarted)
                return;

            listener.Start();
            alreadyStarted = true;
            Console.WriteLine($"JT808 Server is listening on port {_port}...");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("Device connected!");

                Task.Run(() => HandleClient(client));
            }
        }

        private byte[] CreateJT808Response()
        {
            string responseMessage = "JT808 response";
            return Encoding.ASCII.GetBytes(responseMessage);
        }

        public void Stop()
        {
            listener.Stop();
            alreadyStarted = false;
        }

        private async Task HandleClient(TcpClient client)
        {
            SaveDataToJsonFile("Received connection...");
            await semaphore.WaitAsync();

            try
            {
                byte[] buffer = new byte[1024];
                int bytesRead = await client.GetStream().ReadAsync(buffer, 0, buffer.Length);

                if (bytesRead > 0)
                {
                    byte[] receivedData = new byte[bytesRead];
                    Array.Copy(buffer, receivedData, bytesRead);

                    string receivedString = BitConverter.ToString(receivedData);
                    Console.WriteLine($"Received {bytesRead} bytes: {receivedString}");

                    SaveDataToJsonFile(receivedString);

                    byte[] response = CreateJT808Response();
                    await client.GetStream().WriteAsync(response, 0, response.Length);
                    Console.WriteLine("Response sent to the device.");
                }
            }
            finally
            {
                semaphore.Release();
                client.Close();
            }
        }

        private void SaveDataToJsonFile(string receivedString)
        {
            var dataEntry = new
            {
                Timestamp = DateTime.UtcNow,
                Data = receivedString
            };

            lock (fileLock)
            {
                List<object> existingData = new List<object>();

                if (File.Exists(jsonFilePath))
                {
                    string existingJson = File.ReadAllText(jsonFilePath);
                    if (!string.IsNullOrEmpty(existingJson))
                    {
                        existingData = JsonSerializer.Deserialize<List<object>>(existingJson) ?? new List<object>();
                    }
                }

                existingData.Add(dataEntry);

                string newJson = JsonSerializer.Serialize(existingData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(jsonFilePath, newJson);
            }
        }
    }
}

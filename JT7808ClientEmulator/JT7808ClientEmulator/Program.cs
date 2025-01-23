using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace JT808ClientHexReplay
{
    class Program
    {
        static void Main(string[] args)
        {
            // Change this IP and port to match your JT808 server
            string serverIp = "127.0.0.1";
            int serverPort = 2323;

            try
            {
                Console.WriteLine("JT808 Test Client (Hex Replay).");
                Console.WriteLine($"Connecting to {serverIp}:{serverPort} ...");

                // Create TCP client and connect
                TcpClient client = new TcpClient();
                client.Connect(serverIp, serverPort);

                Console.WriteLine("Connected to server. You can now paste your hex messages (without '0x' or spaces).");
                Console.WriteLine("Enter an empty line to exit.\n");

                NetworkStream stream = client.GetStream();

                while (true)
                {
                    Console.WriteLine("Paste your JT808 message in hex (e.g. 7E0200...7E), then press Enter:");
                    string hexInput = Console.ReadLine().Trim();

                    // Break the loop if user enters an empty line
                    if (string.IsNullOrEmpty(hexInput))
                    {
                        break;
                    }

                    try
                    {
                        // Convert hex string to byte array
                        byte[] message = HexStringToByteArray(hexInput);

                        // Send the raw bytes to the server
                        SendMessage(stream, message);

                        // Optionally read response
                        ReadResponse(stream);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing your hex input: {ex.Message}");
                    }
                }

                // Close connection
                stream.Close();
                client.Close();
                Console.WriteLine("Connection closed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection error: {ex.Message}");
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Sends raw bytes to the JT808 server via the established stream.
        /// </summary>
        private static void SendMessage(NetworkStream stream, byte[] message)
        {
            try
            {
                stream.Write(message, 0, message.Length);
                Console.WriteLine($"Sent {message.Length} bytes to server. Hex dump:");
                Console.WriteLine(BitConverter.ToString(message));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending data: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads an incoming response (if any) from the server, up to 2KB, and prints it in hex.
        /// </summary>
        private static void ReadResponse(NetworkStream stream)
        {
            // Wait briefly for data to arrive
            Thread.Sleep(500);

            try
            {
                // Check if there is data to read
                if (stream.DataAvailable)
                {
                    byte[] buffer = new byte[2048];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        byte[] response = new byte[bytesRead];
                        Array.Copy(buffer, response, bytesRead);

                        Console.WriteLine("Received response from server:");
                        Console.WriteLine(BitConverter.ToString(response));
                    }
                    else
                    {
                        Console.WriteLine("No response (0 bytes read).");
                    }
                }
                else
                {
                    Console.WriteLine("No immediate response from server (no data available).");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading response: {ex.Message}");
            }
        }
        /// <summary>
        /// Converts a string of hex digits (possibly containing dashes, spaces, etc.)
        /// into a byte array. Any non [0-9a-fA-F] character is removed.
        /// </summary>
        private static byte[] HexStringToByteArray(string hex)
        {
            // 1) Remove any non-hex characters (including whitespace, dashes, etc.)
            var sb = new StringBuilder();
            foreach (char c in hex)
            {
                if ((c >= '0' && c <= '9') ||
                    (c >= 'A' && c <= 'F') ||
                    (c >= 'a' && c <= 'f'))
                {
                    sb.Append(c);
                }
            }

            // 2) Now sb should contain only hex digits, without spaces or dashes.
            string cleanHex = sb.ToString();

            // 3) Check length
            if (cleanHex.Length % 2 != 0)
            {
                throw new ArgumentException("Hex string must have an even number of valid hex digits.");
            }

            // 4) Convert pairs of hex digits into bytes
            byte[] data = new byte[cleanHex.Length / 2];
            for (int i = 0; i < data.Length; i++)
            {
                string byteValue = cleanHex.Substring(i * 2, 2);
                data[i] = Convert.ToByte(byteValue, 16);
            }

            return data;
        }

    }
}

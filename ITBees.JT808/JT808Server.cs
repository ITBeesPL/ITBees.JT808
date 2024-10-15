using System.Net;
using System.Net.Sockets;
using System.Text;
using ITBees.Interfaces.Platforms;
using Environment = System.Environment;

namespace ITBees.JT808
{
    public class JT808Server :IJT808Server
    {
        private readonly int _port;
        private TcpListener _listener;
        private readonly string _logFilePath = "connection_log.txt";
        private readonly object _fileLock = new object();

        public JT808Server(IPlatformSettingsService platformSettingsService)
        {
            _port = Convert.ToInt32(platformSettingsService.GetSetting("JT808_port"));
            _listener = new TcpListener(IPAddress.Any, _port);
        }

        public async Task StartAsync()
        {
            _listener.Start();
            Console.WriteLine($"JT808 Server is listening on port {_port}...");

            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                Console.WriteLine("Device connected.");

                // Log the connection to a file
                LogConnection(client);

                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private void LogConnection(TcpClient client)
        {
            try
            {
                var remoteEndPoint = client.Client.RemoteEndPoint.ToString();
                var logEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Connection from {remoteEndPoint}";

                lock (_fileLock)
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }

                Console.WriteLine("Connection logged to file.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging connection: {ex.Message}");
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (var networkStream = client.GetStream())
                {
                    var buffer = new byte[1024]; // buffer to hold received bytes
                    var byteList = new List<byte>();

                    while (true)
                    {
                        int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                        {
                            // Client disconnected
                            break;
                        }

                        // Add received bytes to the byte list
                        byteList.AddRange(buffer.Take(bytesRead));

                        if (byteList.Contains(0x7E)) // Detect end flag in the message
                        {
                            // Process the complete message
                            var message = byteList.ToArray();
                            byteList.Clear(); // Clear the byte list for the next message

                            // Process the message
                            var processedMessage = ProcessJT808Message(message);
                            if (processedMessage != null)
                            {
                                // Handle the message and get response
                                var response = HandleMessage(processedMessage);

                                if (response != null)
                                {
                                    // Send response
                                    var responseMessage = CreateJT808Message(response);
                                    await networkStream.WriteAsync(responseMessage, 0, responseMessage.Length);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling client: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }


        private byte[] ProcessJT808Message(byte[] message)
        {
            // Remove start and end flags
            if (message.Length < 2 || message[0] != 0x7E || message[^1] != 0x7E)
            {
                Console.WriteLine("Invalid message framing.");
                return null;
            }

            var content = new byte[message.Length - 2];
            Array.Copy(message, 1, content, 0, content.Length);

            // Unescape data
            content = Unescape(content);

            // Verify checksum
            var checksum = content[^1];
            var data = new byte[content.Length - 1];
            Array.Copy(content, 0, data, 0, data.Length);

            byte calculatedChecksum = 0;
            foreach (var b in data)
            {
                calculatedChecksum ^= b;
            }

            if (checksum != calculatedChecksum)
            {
                Console.WriteLine("Checksum mismatch.");
                return null;
            }

            return data;
        }

        private byte[] Unescape(byte[] data)
        {
            var result = new List<byte>();
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] == 0x7D)
                {
                    if (i + 1 < data.Length)
                    {
                        if (data[i + 1] == 0x01)
                        {
                            result.Add(0x7D);
                            i++;
                        }
                        else if (data[i + 1] == 0x02)
                        {
                            result.Add(0x7E);
                            i++;
                        }
                        else
                        {
                            result.Add(data[i]);
                        }
                    }
                    else
                    {
                        result.Add(data[i]);
                    }
                }
                else
                {
                    result.Add(data[i]);
                }
            }
            return result.ToArray();
        }

        private byte[] Escape(byte[] data)
        {
            var result = new List<byte>();
            foreach (var b in data)
            {
                if (b == 0x7E)
                {
                    result.Add(0x7D);
                    result.Add(0x02);
                }
                else if (b == 0x7D)
                {
                    result.Add(0x7D);
                    result.Add(0x01);
                }
                else
                {
                    result.Add(b);
                }
            }
            return result.ToArray();
        }

        private byte[] CreateJT808Message(byte[] data)
        {
            var message = new List<byte> { 0x7E };

            // Calculate checksum
            byte checksum = 0;
            foreach (var b in data)
            {
                checksum ^= b;
            }

            // Append checksum
            var fullData = new byte[data.Length + 1];
            Array.Copy(data, 0, fullData, 0, data.Length);
            fullData[^1] = checksum;

            // Escape data
            var escapedData = Escape(fullData);
            message.AddRange(escapedData);
            message.Add(0x7E);

            return message.ToArray();
        }

        private byte[] HandleMessage(byte[] data)
        {
            // Parse message header
            if (data.Length < 12)
            {
                Console.WriteLine("Invalid message length.");
                return null;
            }

            ushort msgId = (ushort)((data[0] << 8) + data[1]);
            ushort msgBodyProps = (ushort)((data[2] << 8) + data[3]);

            var phoneNumber = BCDToString(data, 4, 6);
            ushort msgSerialNumber = (ushort)((data[10] << 8) + data[11]);

            var msgBody = new byte[data.Length - 12];
            Array.Copy(data, 12, msgBody, 0, msgBody.Length);

            Console.WriteLine($"Received message ID: 0x{msgId:X4} from device: {phoneNumber}");

            switch (msgId)
            {
                case 0x0100: // Terminal registration
                    return HandleTerminalRegistration(msgSerialNumber, phoneNumber, msgBody);
                case 0x0002: // Heartbeat
                    return HandleHeartbeat(msgSerialNumber, phoneNumber);
                case 0x0200: // Location information report
                    // Handle location data as needed
                    Console.WriteLine("Received location information.");
                    // Send universal response
                    return CreateUniversalResponse(msgSerialNumber, msgId, 0);
                case 0x0001: // Terminal common response
                    Console.WriteLine("Received terminal common response.");
                    break;
                case 0x0102: // Terminal authentication
                    return HandleAuthentication(msgSerialNumber, phoneNumber, msgBody);
                default:
                    Console.WriteLine($"Unknown message ID: 0x{msgId:X4}");
                    // Send universal response with error code
                    return CreateUniversalResponse(msgSerialNumber, msgId, 3);
            }

            return null;
        }

        private byte[] HandleTerminalRegistration(ushort msgSerialNumber, string phoneNumber, byte[] msgBody)
        {
            Console.WriteLine("Handling terminal registration.");

            // Parse registration message body as per protocol
            // For brevity, parsing is omitted here but should be implemented according to the protocol

            // Prepare response message
            ushort responseMsgId = 0x8100;
            var responseBody = new List<byte>();

            // Add response serial number (same as received)
            responseBody.Add((byte)(msgSerialNumber >> 8));
            responseBody.Add((byte)(msgSerialNumber & 0xFF));

            // Add result (0 for success)
            responseBody.Add(0x00);

            // Add authentication code (if necessary)
            var authCode = Encoding.ASCII.GetBytes("AUTH_CODE");
            responseBody.AddRange(authCode);

            // Build response message
            var responseData = BuildJT808Message(responseMsgId, phoneNumber, responseBody.ToArray());

            return responseData;
        }

        private byte[] HandleAuthentication(ushort msgSerialNumber, string phoneNumber, byte[] msgBody)
        {
            Console.WriteLine("Handling authentication.");

            // Process authentication code from msgBody if needed

            // Send universal response indicating success
            return CreateUniversalResponse(msgSerialNumber, 0x0102, 0);
        }

        private byte[] HandleHeartbeat(ushort msgSerialNumber, string phoneNumber)
        {
            Console.WriteLine("Handling heartbeat.");

            // Send universal response indicating success
            return CreateUniversalResponse(msgSerialNumber, 0x0002, 0);
        }

        private byte[] CreateUniversalResponse(ushort msgSerialNumber, ushort msgId, byte result)
        {
            ushort responseMsgId = 0x8001;
            var responseBody = new List<byte>();

            // Add original message serial number
            responseBody.Add((byte)(msgSerialNumber >> 8));
            responseBody.Add((byte)(msgSerialNumber & 0xFF));

            // Add original message ID
            responseBody.Add((byte)(msgId >> 8));
            responseBody.Add((byte)(msgId & 0xFF));

            // Add result code
            responseBody.Add(result);

            // For the universal response, phone number can be empty or same as the device's
            var responseData = BuildJT808Message(responseMsgId, "00000000000", responseBody.ToArray());

            return responseData;
        }

        private byte[] BuildJT808Message(ushort msgId, string phoneNumber, byte[] msgBody)
        {
            var message = new List<byte>();

            // Message ID
            message.Add((byte)(msgId >> 8));
            message.Add((byte)(msgId & 0xFF));

            // Message body properties
            ushort bodyProps = (ushort)msgBody.Length;
            message.Add((byte)(bodyProps >> 8));
            message.Add((byte)(bodyProps & 0xFF));

            // Phone number (BCD encoded)
            var phoneNumberBCD = StringToBCD(phoneNumber, 6);
            message.AddRange(phoneNumberBCD);

            // Message serial number
            ushort msgSerialNumber = GetNextSerialNumber();
            message.Add((byte)(msgSerialNumber >> 8));
            message.Add((byte)(msgSerialNumber & 0xFF));

            // Message body
            message.AddRange(msgBody);

            return message.ToArray();
        }

        private ushort _serialNumber = 0;

        private ushort GetNextSerialNumber()
        {
            _serialNumber++;
            if (_serialNumber == 0)
                _serialNumber = 1;
            return _serialNumber;
        }

        private string BCDToString(byte[] data, int offset, int length)
        {
            var sb = new StringBuilder();
            for (int i = offset; i < offset + length; i++)
            {
                sb.Append((data[i] >> 4).ToString("X"));
                sb.Append((data[i] & 0x0F).ToString("X"));
            }
            return sb.ToString().TrimStart('0');
        }

        private byte[] StringToBCD(string str, int length)
        {
            // Ensure the string has even length
            if (str.Length % 2 != 0)
            {
                str = "0" + str;
            }

            var bcd = new byte[length];
            int strIndex = str.Length - (length * 2);

            for (int i = 0; i < length; i++)
            {
                byte highNibble = 0;
                byte lowNibble = 0;

                if (strIndex >= 0)
                {
                    highNibble = (byte)(str[strIndex] - '0');
                }
                if (strIndex + 1 >= 0)
                {
                    lowNibble = (byte)(str[strIndex + 1] - '0');
                }
                bcd[i] = (byte)((highNibble << 4) | lowNibble);
                strIndex += 2;
            }
            return bcd;
        }
    }
}

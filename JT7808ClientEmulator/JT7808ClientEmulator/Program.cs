using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace JT808ClientEmulator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string serverIp = "127.0.0.1"; // IP address of your JT808 server
            int serverPort = 2323; // Port your JT808 server is listening on

            try
            {
                TcpClient client = new TcpClient();
                client.Connect(serverIp, serverPort);
                Console.WriteLine("Connected to server.");

                NetworkStream stream = client.GetStream();

                // Send registration message
                byte[] registrationMessage = BuildRegistrationMessage();
                SendMessage(stream, registrationMessage, "registration");

                // Send authentication message
                byte[] authMessage = BuildAuthenticationMessage();
                SendMessage(stream, authMessage, "authentication");

                // Send location report message
                byte[] locationMessage = BuildLocationReportMessage();
                SendMessage(stream, locationMessage, "location report");

                // Optionally, send custom messages
                while (true)
                {
                    Console.WriteLine("Do you want to send a custom message? (y/n)");
                    string input = Console.ReadLine();
                    if (input.ToLower() != "y")
                        break;

                    Console.WriteLine("Enter the message in hex format (e.g., 7E020000...):");
                    string hexMessage = Console.ReadLine();
                    byte[] customMessage = Convert.FromBase64String(hexMessage);
                    SendMessage(stream, customMessage, "custom message");
                }

                // Close the connection
                stream.Close();
                client.Close();
                Console.WriteLine("Connection closed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static void SendMessage(NetworkStream stream, byte[] message, string messageType)
        {
            // Send message to the server
            stream.Write(message, 0, message.Length);
            Console.WriteLine($"Sent {messageType} message.");

            // Receive response from the server
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            Console.WriteLine("Received response from server:");

            // Display response in hexadecimal format
            Console.WriteLine(BitConverter.ToString(buffer, 0, bytesRead));

            // Optional: Add delay between messages
            Thread.Sleep(1000);
        }

        static byte[] BuildRegistrationMessage()
        {
            // Construct registration message according to JT808 protocol
            ushort msgId = 0x0100;

            // Message Body
            List<byte> body = new List<byte>();

            // Province ID (2 bytes)
            ushort provinceId = 0x0033;
            body.Add((byte)(provinceId >> 8));
            body.Add((byte)(provinceId & 0xFF));

            // City ID (2 bytes)
            ushort cityId = 0x0044;
            body.Add((byte)(cityId >> 8));
            body.Add((byte)(cityId & 0xFF));

            // Manufacturer ID (5 bytes)
            string manufacturerId = "ABCDE";
            byte[] manufacturerIdBytes = Encoding.ASCII.GetBytes(manufacturerId.PadRight(5, '\0'));
            body.AddRange(manufacturerIdBytes);

            // Terminal Model (20 bytes)
            string terminalModel = "JT808Client";
            byte[] terminalModelBytes = Encoding.ASCII.GetBytes(terminalModel.PadRight(20, '\0'));
            body.AddRange(terminalModelBytes);

            // Terminal ID (7 bytes)
            string terminalId = "1234567";
            byte[] terminalIdBytes = Encoding.ASCII.GetBytes(terminalId.PadRight(7, '\0'));
            body.AddRange(terminalIdBytes);

            // License Plate Color (1 byte)
            byte plateColor = 0x01; // Blue
            body.Add(plateColor);

            //// License Plate (variable, GBK encoding)
            //string plateNumber = "TEST123";
            //byte[] plateNumberBytes = Encoding.GetEncoding("GBK").GetBytes(plateNumber);
            //body.AddRange(plateNumberBytes);

            byte[] bodyBytes = body.ToArray();

            // Build the full message
            byte[] message = BuildJT808Message(msgId, bodyBytes, 1);

            return message;
        }

        static byte[] BuildAuthenticationMessage()
        {
            // Construct authentication message according to JT808 protocol
            ushort msgId = 0x0102;

            // Message Body
            string authCode = "AUTH_CODE"; // Authentication code
            byte[] authCodeBytes = Encoding.ASCII.GetBytes(authCode);

            // Build the full message
            byte[] message = BuildJT808Message(msgId, authCodeBytes, 2);

            return message;
        }

        static byte[] BuildLocationReportMessage()
        {
            // Construct location report message according to JT808 protocol
            ushort msgId = 0x0200;

            // Message Body
            List<byte> body = new List<byte>();

            // Alarm flags (4 bytes)
            uint alarmFlags = 0x00000000;
            body.AddRange(BitConverter.GetBytes(alarmFlags).Reverse());

            // Status flags (4 bytes)
            uint statusFlags = 0x00000000;
            body.AddRange(BitConverter.GetBytes(statusFlags).Reverse());

            // Latitude (4 bytes)
            Console.WriteLine("Enter latitude (e.g., 51.057589):");
            double latitudeInput = double.Parse(Console.ReadLine());
            uint latitude = (uint)(latitudeInput * 1e6);
            body.AddRange(BitConverter.GetBytes(latitude).Reverse());

            // Longitude (4 bytes)
            Console.WriteLine("Enter longitude (e.g., 17.032861):");
            double longitudeInput = double.Parse(Console.ReadLine());
            uint longitude = (uint)(longitudeInput * 1e6);
            body.AddRange(BitConverter.GetBytes(longitude).Reverse());

            // Altitude (2 bytes)
            ushort altitude = 10; // In meters
            body.Add((byte)(altitude >> 8));
            body.Add((byte)(altitude & 0xFF));

            // Speed (2 bytes)
            ushort speed = 60; // In km/h
            body.Add((byte)(speed >> 8));
            body.Add((byte)(speed & 0xFF));

            // Direction (2 bytes)
            ushort direction = 90; // In degrees
            body.Add((byte)(direction >> 8));
            body.Add((byte)(direction & 0xFF));

            // Time (6 bytes), BCD[6], YYMMDDhhmmss
            string time = DateTime.Now.ToString("yyMMddHHmmss");
            byte[] timeBytes = StringToBCD(time);
            body.AddRange(timeBytes);

            // Additional information (optional)
            // Uncomment and modify as needed
            /*
            // Mileage (0x01)
            body.Add(0x01); // Info ID
            body.Add(0x04); // Length
            uint mileage = 12345; // In meters
            body.AddRange(BitConverter.GetBytes(mileage).Reverse());

            // Fuel (0x02)
            body.Add(0x02); // Info ID
            body.Add(0x02); // Length
            ushort fuel = 50; // In liters
            body.Add((byte)(fuel >> 8));
            body.Add((byte)(fuel & 0xFF));
            */

            byte[] bodyBytes = body.ToArray();

            // Build the full message
            byte[] message = BuildJT808Message(msgId, bodyBytes, 3);

            return message;
        }

        static byte[] BuildJT808Message(ushort msgId, byte[] bodyBytes, ushort msgSerialNumber)
        {
            // Device ID (BCD[6])
            string deviceId = "123456789012"; // Example device ID (12 digits)
            byte[] deviceIdBCD = StringToBCD(deviceId);

            // Message Body Properties
            ushort msgBodyProps = (ushort)(bodyBytes.Length & 0x3FF); // Lower 10 bits for length
            msgBodyProps |= 0x0000; // Set encryption and subpackage bits if needed

            // Build the message
            List<byte> message = new List<byte>();

            // Add Message Header
            message.Add((byte)(msgId >> 8));
            message.Add((byte)(msgId & 0xFF));
            message.Add((byte)(msgBodyProps >> 8));
            message.Add((byte)(msgBodyProps & 0xFF));
            message.AddRange(deviceIdBCD);
            message.Add((byte)(msgSerialNumber >> 8));
            message.Add((byte)(msgSerialNumber & 0xFF));

            // Add Message Body
            message.AddRange(bodyBytes);

            // Calculate checksum
            byte checksum = 0;
            for (int i = 0; i < message.Count; i++)
            {
                checksum ^= message[i];
            }

            // Perform escaping
            List<byte> escapedMessage = new List<byte>();
            escapedMessage.Add(0x7E); // Start flag
            foreach (byte b in message)
            {
                if (b == 0x7E)
                {
                    escapedMessage.Add(0x7D);
                    escapedMessage.Add(0x02);
                }
                else if (b == 0x7D)
                {
                    escapedMessage.Add(0x7D);
                    escapedMessage.Add(0x01);
                }
                else
                {
                    escapedMessage.Add(b);
                }
            }

            // Add checksum
            if (checksum == 0x7E)
            {
                escapedMessage.Add(0x7D);
                escapedMessage.Add(0x02);
            }
            else if (checksum == 0x7D)
            {
                escapedMessage.Add(0x7D);
                escapedMessage.Add(0x01);
            }
            else
            {
                escapedMessage.Add(checksum);
            }

            escapedMessage.Add(0x7E); // End flag

            return escapedMessage.ToArray();
        }

        static byte[] StringToBCD(string str)
        {
            if (str.Length % 2 != 0)
            {
                str = "0" + str;
            }

            byte[] bcd = new byte[str.Length / 2];
            for (int i = 0; i < bcd.Length; i++)
            {
                bcd[i] = (byte)(((GetHexValue(str[2 * i]) << 4) | GetHexValue(str[2 * i + 1])));
            }
            return bcd;
        }

        static int GetHexValue(char hex)
        {
            if (hex >= '0' && hex <= '9')
                return hex - '0';
            else if (hex >= 'A' && hex <= 'F')
                return hex - 'A' + 10;
            else if (hex >= 'a' && hex <= 'f')
                return hex - 'a' + 10;
            else
                throw new ArgumentException("Invalid hexadecimal character");
        }

        static byte[] HexStringToByteArray(string hex)
        {
            hex = hex.Replace(" ", "");
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Hex string must have an even length");
            byte[] data = new byte[hex.Length / 2];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (byte)((GetHexValue(hex[2 * i]) << 4) | GetHexValue(hex[2 * i + 1]));
            }
            return data;
        }

        static byte[] StringToBCD(string str, int length)
        {
            // Pads or trims the string to the desired length
            if (str.Length > length * 2)
                str = str.Substring(str.Length - length * 2);
            else if (str.Length < length * 2)
                str = str.PadLeft(length * 2, '0');

            return StringToBCD(str);
        }
    }
}

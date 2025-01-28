using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using ITBees.Interfaces.Platforms;
using ITBees.JT808.Interfaces;
using Microsoft.Extensions.Logging;

namespace JT808ServerApp
{
    public class JT808Server<T> : IJT808Server<T> where T : GpsData, new()
    {
        private readonly IPlatformSettingsService _platformSettingsService;
        private readonly IGpsWriteRequestLogSingleton<T> _gpsWriteRequestLogSingleton;
        private readonly IGpsDeviceAuthorizationSingleton _gpsDeviceAuthorizationSingleton;
        private readonly ILogger<JT808Server<T>> _logger;
        private readonly int _port;
        private TcpListener _listener;
        private readonly object _fileLock = new object();
        private Dictionary<string, TcpClient> _authorizedDevices = new Dictionary<string, TcpClient>();
        private ushort _serialNumber = 0;

        public JT808Server(
            IPlatformSettingsService platformSettingsService,
            IGpsWriteRequestLogSingleton<T> gpsWriteRequestLogSingleton,
            IGpsDeviceAuthorizationSingleton gpsDeviceAuthorizationSingleton,
            ILogger<JT808Server<T>> logger)
        {
            // Register code page provider for GBK or other encodings if needed
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            _platformSettingsService = platformSettingsService;
            _gpsWriteRequestLogSingleton = gpsWriteRequestLogSingleton;
            _gpsDeviceAuthorizationSingleton = gpsDeviceAuthorizationSingleton;
            _logger = logger;

            // Read port number from platform settings
            _port = Convert.ToInt32(_platformSettingsService.GetSetting("JT808_port"));
            _listener = new TcpListener(IPAddress.Any, _port);

            _logger.LogInformation($"Created JT808 server on port: {_port}");
        }

        public async Task StartAsync()
        {
            var format = $"Start async - JT808 server on port {_port}";
            Console.WriteLine(format);
            _logger.LogInformation(format);

            _listener.Start();

            while (true)
            {
                var value = $"Received request {DateTime.Now}";
                Console.WriteLine(value);
                _logger.LogInformation(value);

                // Accept incoming TCP client
                var client = await _listener.AcceptTcpClientAsync();
                // Handle client in a separate task
                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (var networkStream = client.GetStream())
                {
                    var buffer = new byte[1024];
                    var byteList = new List<byte>();

                    while (true)
                    {
                        // Read data from the socket
                        int bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                            break;

                        byteList.AddRange(buffer.Take(bytesRead));
                        byte[] rawBytes = byteList.ToArray();

                        // Convert to hex string just for logging/analysis
                        string hexData = ToHexString(rawBytes);

                        // We search for 0x7E...0x7E frames (start/end markers)
                        while (byteList.Contains(0x7E))
                        {
                            int startIndex = byteList.IndexOf(0x7E);
                            int endIndex = byteList.IndexOf(0x7E, startIndex + 1);
                            if (endIndex == -1)
                                break;

                            var messageBytes = byteList
                                .Skip(startIndex)
                                .Take(endIndex - startIndex + 1)
                                .ToArray();

                            // Remove consumed bytes from the list
                            byteList.RemoveRange(0, endIndex + 1);

                            Console.WriteLine($"Received message (hex): {BitConverter.ToString(messageBytes)}");
                            string base64Message = Convert.ToBase64String(messageBytes);
                            Console.WriteLine($"Received message (Base64): {base64Message}");

                            // Process JT808 message (unescape, checksum, etc.)
                            var data = ProcessJT808Message(messageBytes);
                            if (data != null)
                            {
                                var response = HandleMessage(data, client, base64Message, hexData);
                                if (response != null)
                                {
                                    await networkStream.WriteAsync(response, 0, response.Length);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                client.Close();
            }
        }

        private string ToHexString(byte[] data)
        {
            var sb = new StringBuilder(data.Length * 2);
            foreach (var b in data)
            {
                sb.Append(b.ToString("X2"));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Process the raw JT808 data (remove 0x7E markers, unescape, verify checksum).
        /// </summary>
        private byte[] ProcessJT808Message(byte[] message, string hexData = null)
        {
            // Check length and boundary
            if (message.Length < 2 || message[0] != 0x7E || message[message.Length - 1] != 0x7E)
                return null;

            // Extract content between 0x7E ... 0x7E
            var content = new byte[message.Length - 2];
            Array.Copy(message, 1, content, 0, content.Length);

            // Unescape (0x7D 0x02 -> 0x7E, etc.)
            content = Unescape(content);

            // Last byte is checksum
            var checksum = content[content.Length - 1];
            var data = new byte[content.Length - 1];
            Array.Copy(content, 0, data, 0, data.Length);

            // Calculate our own checksum
            byte calculatedChecksum = 0;
            foreach (var b in data)
            {
                calculatedChecksum ^= b;
            }

            if (checksum != calculatedChecksum)
            {
                _logger.LogWarning("Checksum mismatch!");
                return null;
            }

            return data;
        }

        /// <summary>
        /// Unescape the JT808 data according to the 0x7D rules.
        /// </summary>
        private byte[] Unescape(byte[] data)
        {
            var result = new List<byte>();
            int i = 0;
            while (i < data.Length)
            {
                if (data[i] == 0x7D)
                {
                    // If next byte is 0x01 -> 0x7D, if 0x02 -> 0x7E
                    if (i + 1 < data.Length)
                    {
                        if (data[i + 1] == 0x02)
                        {
                            result.Add(0x7E);
                            i += 2;
                        }
                        else if (data[i + 1] == 0x01)
                        {
                            result.Add(0x7D);
                            i += 2;
                        }
                        else
                        {
                            // Not a known escape sequence, keep the 0x7D
                            result.Add(data[i]);
                            i++;
                        }
                    }
                    else
                    {
                        // 0x7D is the last byte, just add it
                        result.Add(data[i]);
                        i++;
                    }
                }
                else
                {
                    result.Add(data[i]);
                    i++;
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// Escape (reverse of Unescape) for outgoing messages.
        /// </summary>
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

        /// <summary>
        /// Top-level handler for the recognized and parsed JT808 data.
        /// </summary>
        private byte[] HandleMessage(byte[] data, TcpClient client, string base64Message, string hexData)
        {
            // Keep a base64 string of the entire content for logs
            string base64String = Convert.ToBase64String(data);

            // Create T-type object and fill some properties
            var gpsData = new T()
            {
                RequestBody = "",
                Received = DateTime.Now,
                MessageHex = hexData
            };

            try
            {
                if (data.Length < 12)
                    return null;

                // Standard JT808 header fields (after the 0x7E ... unescaped data)
                ushort msgId = ReadUInt16BigEndian(data, 0);
                ushort msgBodyProps = ReadUInt16BigEndian(data, 2);
                string deviceId = BCDToString(data, 4, 6);
                ushort msgSerialNumber = ReadUInt16BigEndian(data, 10);

                gpsData.DeviceId = deviceId;

                var msgBody = new byte[data.Length - 12];
                Array.Copy(data, 12, msgBody, 0, msgBody.Length);

                switch (msgId)
                {
                    case 0x0100: // Terminal Registration
                        return HandleTerminalRegistration(msgSerialNumber, deviceId, msgBody, gpsData);

                    case 0x0102: // Authentication
                        return HandleAuthentication(msgSerialNumber, deviceId, msgBody, gpsData);

                    case 0x0002: // Heartbeat
                        gpsData.RequestBody = "Heartbeat " + gpsData.RequestBody;
                        _gpsWriteRequestLogSingleton.UpdateHeartBeat(gpsData);
                        return CreateUniversalResponse(msgSerialNumber, msgId, 0, deviceId);

                    case 0x0200: // Location report
                        HandleLocationReport(deviceId, msgBody, gpsData);
                        return CreateUniversalResponse(msgSerialNumber, msgId, 0, deviceId);

                    case 0x0001:
                        gpsData.RequestBody = "Break - 0x0001 " + gpsData.RequestBody;
                        _gpsWriteRequestLogSingleton.WriteUnknownMessages(gpsData);
                        break;

                    case 0x0300: // Text message
                        HandleTextMessage(deviceId, msgBody, gpsData);
                        return CreateUniversalResponse(msgSerialNumber, msgId, 0, deviceId);

                    case 0x0500: // Control response
                        HandleControlResponse(deviceId, msgBody, gpsData);
                        return CreateUniversalResponse(msgSerialNumber, msgId, 0, deviceId);

                    case 0x0F01: // Custom time sync request
                        return HandleTimeSyncRequest(msgSerialNumber, deviceId, gpsData);

                    case 0x0900: // Data Upstream Pass Through
                        HandleDataUpstreamPassThrough(deviceId, msgBody, gpsData);
                        return CreateUniversalResponse(msgSerialNumber, msgId, 0, deviceId);

                    default:
                        gpsData.RequestBody = "Default not handled case " + gpsData.RequestBody;
                        _gpsWriteRequestLogSingleton.WriteUnknownMessages(gpsData);
                        return CreateUniversalResponse(msgSerialNumber, msgId, 3, deviceId);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, base64String);
                throw;
            }
        }

        /// <summary>
        /// Handle data pass-through (e.g. VIN data, etc.).
        /// </summary>
        private void HandleDataUpstreamPassThrough(string deviceId, byte[] msgBody, T gpsData)
        {
            byte type = msgBody[0];
            byte[] content = msgBody.Skip(1).ToArray();

            if (type == 0xF8)
            {
                string contentString = Encoding.GetEncoding("GBK").GetString(content);

                // Example: searching for VIN
                string vinPattern = @"[A-HJ-NPR-Z0-9]{17}";
                var matches = Regex.Matches(contentString, vinPattern);
                if (matches.Count > 0)
                {
                    string vin = matches[0].Value;
                    if (vin.StartsWith("R"))
                    {
                        vin = vin.Substring(1);
                    }
                    gpsData.VIN = vin;
                    _gpsWriteRequestLogSingleton.ExtractedVin(vin, gpsData);
                }

                gpsData.RequestBody =
                    $"HandleDataUpstreamPassThrough type={type:X2}, Content (string): {contentString} " 
                    + gpsData.RequestBody;
                gpsData.StartJourney = true;
            }
            else
            {
                gpsData.RequestBody =
                    $"HandleDataUpstreamPassThrough type={type:X2}, Content (hex): {BitConverter.ToString(content)} "
                    + gpsData.RequestBody;
            }

            _gpsWriteRequestLogSingleton.WriteUnknownMessages(gpsData);
        }

        /// <summary>
        /// Handle terminal registration (msgId = 0x0100).
        /// </summary>
        private byte[] HandleTerminalRegistration(ushort msgSerialNumber, string deviceId, byte[] msgBody, T gpsData)
        {
            gpsData.RequestBody = "HandleTerminalRegistration " + gpsData.RequestBody;

            int index = 0;

            ushort provinceId = ReadUInt16BigEndian(msgBody, index);
            index += 2;

            ushort cityId = ReadUInt16BigEndian(msgBody, index);
            index += 2;

            string manufacturerId = Encoding.ASCII.GetString(msgBody, index, 5).Trim('\0');
            index += 5;

            string terminalModel = Encoding.ASCII.GetString(msgBody, index, 20).Trim('\0');
            index += 20;

            string terminalId = Encoding.ASCII.GetString(msgBody, index, 7).Trim('\0');
            index += 7;

            byte plateColor = msgBody[index];
            index += 1;

            // Optionally read VIN if there's leftover in msgBody
            // Example:
            // if (index < msgBody.Length)
            // {
            //     string vin = Encoding.GetEncoding("GBK")
            //                      .GetString(msgBody, index, msgBody.Length - index)
            //                      .Trim('\0');
            //     gpsData.VIN = vin;
            // }

            gpsData.ManufacturerId = manufacturerId;
            gpsData.TerminalModel = terminalModel;
            gpsData.TerminalId = terminalId;
            gpsData.PlateColor = plateColor;

            _logger.LogInformation(
                $"Terminal Registered: DeviceId={deviceId}, TerminalId={terminalId}, ManufacturerId={manufacturerId}");

            // Build the 0x8100 response
            ushort responseMsgId = 0x8100;
            var responseBody = new List<byte>();

            // 1) Original msg serial
            responseBody.Add((byte)(msgSerialNumber >> 8));
            responseBody.Add((byte)(msgSerialNumber & 0xFF));

            // 2) Result: 0=success, 1=fail, 2=vehicle already registered, ...
            var isAuthorized = 
                _gpsDeviceAuthorizationSingleton.IsAuthorized(deviceId, terminalModel, terminalId, gpsData.VIN);

            if (isAuthorized) responseBody.Add(0x00);
            else responseBody.Add(0x01);

            // 3) Auth code
            string authCode = "AUTH_CODE";
            byte[] authCodeBytes = Encoding.ASCII.GetBytes(authCode);
            responseBody.AddRange(authCodeBytes);

            var responseData = BuildJT808Message(responseMsgId, deviceId, responseBody.ToArray());
            _gpsWriteRequestLogSingleton.HandleTerminalRegistration(gpsData);

            return responseData;
        }

        /// <summary>
        /// Handle authentication (0x0102).
        /// </summary>
        private byte[] HandleAuthentication(ushort msgSerialNumber, string deviceId, byte[] msgBody, T gpsData)
        {
            var authCode = Encoding.ASCII.GetString(msgBody);
            gpsData.RequestBody = "Handle authentication " + gpsData.RequestBody;
            _gpsWriteRequestLogSingleton.HandleAuthentication(gpsData);

            // Return universal response
            return CreateUniversalResponse(msgSerialNumber, 0x0102, 0, deviceId);
        }

        /// <summary>
        /// Handle location report (0x0200).
        /// This is where we do the main fix for direction masking and BCD-time fallback.
        /// </summary>
        private void HandleLocationReport(string deviceId, byte[] msgBody, T gpsData)
        {
            // 1) Read standard JT808 fields
            uint alarmFlag = ReadUInt32BigEndian(msgBody, 0);
            uint status = ReadUInt32BigEndian(msgBody, 4);
            uint latitude = ReadUInt32BigEndian(msgBody, 8);
            uint longitude = ReadUInt32BigEndian(msgBody, 12);
            ushort altitude = ReadUInt16BigEndian(msgBody, 16);
            ushort speed = ReadUInt16BigEndian(msgBody, 18);

            // direction might contain extra bits in the high order, so let's mask it
            ushort rawDirection = ReadUInt16BigEndian(msgBody, 20);
            ushort maskedDirection = (ushort)(rawDirection & 0x01FF); 
            // Some devices use 0x03FF mask. Adjust if needed.

            // We'll store the masked direction:
            gpsData.Direction = maskedDirection;

            // Next 6 bytes are supposed to be time in BCD (YYMMDDHHmmss),
            // but some devices send zeros or invalid data. Let's handle that gracefully.
            string rawTimeBCD = BCDToString(msgBody, 22, 6);
            
            // We'll try to parse it. If it fails or is all zeros, we use a fallback.
            if (IsAllZeroBCD(rawTimeBCD))
            {
                gpsData.Timestamp = DateTime.UtcNow; // fallback
            }
            else
            {
                try
                {
                    gpsData.Timestamp = DateTime.ParseExact(rawTimeBCD, "yyMMddHHmmss", null);
                }
                catch
                {
                    // fallback if parse fails
                    gpsData.Timestamp = DateTime.UtcNow;
                }
            }

            // Assign the basic fields
            gpsData.DeviceId = deviceId;
            gpsData.Latitude = latitude / 1e6;
            gpsData.Longitude = longitude / 1e6;
            gpsData.Altitude = altitude;

            // Speed might be 1/10 km/h or direct. Typically JT808 uses 0.1 km/h
            gpsData.Speed = speed / 10.0;

            gpsData.AlarmFlag = alarmFlag;
            gpsData.Status = status;

            // 2) Check ACC (bit 0 of status), if you want
            bool isEngineOn = (status & 0x00000001) != 0;
            gpsData.IsEngineOn = isEngineOn;

            _logger.LogInformation($"[{deviceId}] Location reported: lat={gpsData.Latitude}, lon={gpsData.Longitude}, " +
                                   $"speed={gpsData.Speed}, direction={gpsData.Direction}, engineOn={isEngineOn}");

            gpsData.RequestBody = $"Handle location report (dirRaw={rawDirection}, dirMasked={maskedDirection}) " + 
                                  gpsData.RequestBody;

            // 3) Parse additional data if it exists
            byte[] additionalData = msgBody.Skip(28).ToArray();
            ParseAdditionalData(additionalData, gpsData);

            // 4) Log or store
            _gpsWriteRequestLogSingleton.Write(gpsData);
        }

        /// <summary>
        /// Helper method to check if time BCD is all zeros (e.g. "000000000000").
        /// </summary>
        private bool IsAllZeroBCD(string bcdString)
        {
            if (string.IsNullOrEmpty(bcdString)) return true;
            foreach (char c in bcdString)
            {
                if (c != '0') return false;
            }
            return true;
        }

        /// <summary>
        /// Parse Additional Location Data (ID, length, value...).
        /// </summary>
        private void ParseAdditionalData(byte[] data, GpsData gpsData)
        {
            int index = 0;
            while (index < data.Length)
            {
                if (index + 2 > data.Length)
                    break;

                byte infoId = data[index++];
                byte infoLength = data[index++];

                if (index + infoLength > data.Length)
                    break;

                byte[] infoContent = data.Skip(index).Take(infoLength).ToArray();
                index += infoLength;

                switch (infoId)
                {
                    case 0x01:
                        // Typically total mileage, 4 bytes
                        gpsData.Mileage = ReadUInt32BigEndian(infoContent, 0) / 10.0;
                        break;
                    case 0x25:
                        // Just an example
                        gpsData.ExtendedStatus = ReadUInt32BigEndian(infoContent, 0);
                        break;
                    case 0x2A:
                        // IO status, 2 bytes
                        gpsData.IOStatus = ReadUInt16BigEndian(infoContent, 0);
                        break;
                    case 0x30:
                        // Network signal, 1 byte
                        gpsData.NetworkSignal = infoContent[0];
                        break;
                    case 0x31:
                        // Satellites count, 1 byte
                        gpsData.Satellites = infoContent[0];
                        break;
                    case 0xE3:
                        // Example for battery voltage
                        gpsData.BatteryVoltage = ReadUInt16BigEndian(infoContent, 0) * 0.001;
                        break;
                    default:
                        // Unknown or not handled
                        break;
                }
            }
        }

        /// <summary>
        /// Handle text message (0x0300).
        /// </summary>
        private void HandleTextMessage(string deviceId, byte[] msgBody, T gpsData)
        {
            var message = Encoding.GetEncoding("GBK").GetString(msgBody);
            gpsData.RequestBody = "Handle text message " + gpsData.RequestBody;
            _gpsWriteRequestLogSingleton.WriteUnknownMessages(gpsData);
        }

        /// <summary>
        /// Handle control response (0x0500).
        /// </summary>
        private void HandleControlResponse(string deviceId, byte[] msgBody, GpsData gpsData)
        {
            gpsData.RequestBody = "Handle control response " + gpsData.RequestBody;
        }

        /// <summary>
        /// Handle time sync request (example custom ID 0x0F01).
        /// </summary>
        private byte[] HandleTimeSyncRequest(ushort msgSerialNumber, string deviceId, T gpsData)
        {
            ushort responseMsgId = 0x8F01;
            var responseBody = new List<byte>();

            // e.g. 0x01 means we are responding with time
            responseBody.Add(0x01);

            // We provide a BCD time
            var time = DateTime.UtcNow.ToString("yyMMddHHmmss");
            var timeBytes = StringToBCD(time, 6);
            responseBody.AddRange(timeBytes);

            var responseData = BuildJT808Message(responseMsgId, deviceId, responseBody.ToArray());
            gpsData.RequestBody = "Handle time sync request " + gpsData.RequestBody;
            _gpsWriteRequestLogSingleton.WriteTimeSyncRequest(gpsData);

            return responseData;
        }

        /// <summary>
        /// Create universal response (0x8001).
        /// </summary>
        private byte[] CreateUniversalResponse(ushort msgSerialNumber, ushort msgId, byte result, string deviceId)
        {
            // 0x8001 is the general platform response
            ushort responseMsgId = 0x8001;
            var responseBody = new List<byte>();

            // 1) original message serial
            responseBody.Add((byte)(msgSerialNumber >> 8));
            responseBody.Add((byte)(msgSerialNumber & 0xFF));

            // 2) original message ID
            responseBody.Add((byte)(msgId >> 8));
            responseBody.Add((byte)(msgId & 0xFF));

            // 3) result code: 0=success, 1=failure, etc.
            responseBody.Add(result);

            var responseData = BuildJT808Message(responseMsgId, deviceId, responseBody.ToArray());
            return responseData;
        }

        /// <summary>
        /// Build a JT808 message for sending back to the device.
        /// </summary>
        private byte[] BuildJT808Message(ushort msgId, string deviceId, byte[] msgBody)
        {
            var message = new List<byte>();

            // 1) message ID (2 bytes)
            message.Add((byte)(msgId >> 8));
            message.Add((byte)(msgId & 0xFF));

            // 2) body properties (just the length here, plus we assume no encryption)
            ushort bodyProps = (ushort)msgBody.Length;
            message.Add((byte)(bodyProps >> 8));
            message.Add((byte)(bodyProps & 0xFF));

            // 3) device ID in BCD (6 bytes)
            var deviceIdBCD = StringToBCD(deviceId, 6);
            message.AddRange(deviceIdBCD);

            // 4) message serial number
            ushort msgSerialNumber = GetNextSerialNumber();
            message.Add((byte)(msgSerialNumber >> 8));
            message.Add((byte)(msgSerialNumber & 0xFF));

            // 5) message body
            message.AddRange(msgBody);

            // 6) compute checksum (XOR of all above)
            byte checksum = 0;
            foreach (var b in message)
            {
                checksum ^= b;
            }

            // 7) final: wrap with 0x7E, do escaping
            var fullMessage = new List<byte> { 0x7E };
            var escapedData = Escape(message.ToArray());
            fullMessage.AddRange(escapedData);
            fullMessage.Add(checksum);
            fullMessage.Add(0x7E);

            return fullMessage.ToArray();
        }

        private ushort GetNextSerialNumber()
        {
            // Simple increment, never 0
            _serialNumber++;
            if (_serialNumber == 0)
                _serialNumber = 1;
            return _serialNumber;
        }

        /// <summary>
        /// Convert BCD-coded bytes to a string. 
        /// For example, 0x12 0x34 becomes "1234".
        /// </summary>
        private string BCDToString(byte[] data, int offset, int length)
        {
            var sb = new StringBuilder();
            for (int i = offset; i < offset + length; i++)
            {
                // High nibble
                sb.Append((data[i] >> 4).ToString("X"));
                // Low nibble
                sb.Append((data[i] & 0x0F).ToString("X"));
            }
            // .TrimStart('0') might remove leading zeros if you want 
            // but sometimes you might want the full string.
            return sb.ToString();
        }

        /// <summary>
        /// Convert string (e.g. "123456") to BCD-coded bytes.
        /// </summary>
        private byte[] StringToBCD(string str, int length)
        {
            if (str.Length % 2 != 0)
                str = "0" + str;

            var bcd = new byte[length];
            int strIndex = 0;

            for (int i = 0; i < length; i++)
            {
                byte highNibble = 0;
                byte lowNibble = 0;

                if (strIndex < str.Length)
                    highNibble = (byte)(str[strIndex++] - '0');
                if (strIndex < str.Length)
                    lowNibble = (byte)(str[strIndex++] - '0');

                bcd[i] = (byte)((highNibble << 4) | lowNibble);
            }

            return bcd;
        }

        /// <summary>
        /// Read 4 bytes in big-endian order and convert to uint.
        /// </summary>
        private uint ReadUInt32BigEndian(byte[] buffer, int offset)
        {
            return (uint)(
                (buffer[offset] << 24) |
                (buffer[offset + 1] << 16) |
                (buffer[offset + 2] << 8) |
                buffer[offset + 3]
            );
        }

        /// <summary>
        /// Read 2 bytes in big-endian order and convert to ushort.
        /// </summary>
        private ushort ReadUInt16BigEndian(byte[] buffer, int offset)
        {
            return (ushort)(
                (buffer[offset] << 8) |
                buffer[offset + 1]
            );
        }
    }
}